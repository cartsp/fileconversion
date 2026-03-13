/**
 * Core review logic and Z AI API integration
 */

const { buildSystemPrompt, buildUserMessage, buildTruncatedPrompt } = require('./prompts');
const { retryWithBackoff, truncateText, extractFilesFromDiff, shouldExcludeFile } = require('./utils');
const { parseResponse, processFindings } = require('./parser');

const ZAI_API_URL = 'https://api.z.ai/api/coding/paas/v4/chat/completions';
const MAX_DIFF_SIZE = 100000; // 100KB max diff size
const DEFAULT_MODEL = 'GLM-5';

/**
 * Filter diff content by exclude patterns
 */
function filterDiffByPatterns(diff, excludePatterns) {
  if (!excludePatterns || excludePatterns.length === 0) {
    return { filtered: diff, excludedFiles: [] };
  }

  const files = extractFilesFromDiff(diff);
  const excludedFiles = files.filter(f => shouldExcludeFile(f, excludePatterns));

  if (excludedFiles.length === 0) {
    return { filtered: diff, excludedFiles: [] };
  }

  // Split diff by file and filter
  const fileDiffs = diff.split(/(?=diff --git )/);
  const filteredDiffs = fileDiffs.filter(fileDiff => {
    const match = fileDiff.match(/diff --git a\/.+ b\/(.+?)[\s\n]/);
    if (match) {
      const filename = match[1].trim();
      return !shouldExcludeFile(filename, excludePatterns);
    }
    return true;
  });

  return {
    filtered: filteredDiffs.join(''),
    excludedFiles
  };
}

/**
 * Call Z AI API
 */
async function callZaiApi(apiKey, model, systemPrompt, userMessage, options = {}) {
  const { temperature = 0.3, maxTokens = 4096 } = options;

  const response = await fetch(ZAI_API_URL, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${apiKey}`
    },
    body: JSON.stringify({
      model: model || DEFAULT_MODEL,
      messages: [
        { role: 'system', content: systemPrompt },
        { role: 'user', content: userMessage }
      ],
      temperature,
      max_tokens: maxTokens
    })
  });

  if (!response.ok) {
    const errorText = await response.text();
    const error = new Error(`Z AI API error: ${response.status} - ${errorText}`);
    error.status = response.status;
    throw error;
  }

  const data = await response.json();

  if (!data.choices || !data.choices[0] || !data.choices[0].message) {
    throw new Error('Invalid response structure from Z AI API');
  }

  return data.choices[0].message.content;
}

/**
 * Perform code review using Z AI
 */
async function performReview(diff, options = {}) {
  const {
    apiKey,
    model = DEFAULT_MODEL,
    reviewType = 'full',
    focusAreas = [],
    projectContext = '',
    excludePatterns = [],
    severityThreshold = 'low',
    maxComments = 25
  } = options;

  // Validate API key
  if (!apiKey) {
    throw new Error('Z AI API key is required');
  }

  // Filter diff by exclude patterns
  const { filtered, excludedFiles } = filterDiffByPatterns(diff, excludePatterns);

  if (excludedFiles.length > 0) {
    console.log(`Excluded ${excludedFiles.length} files: ${excludedFiles.join(', ')}`);
  }

  // Check if diff is empty after filtering
  if (!filtered || filtered.trim().length === 0) {
    return {
      findings: [{
        severity: 'INFO',
        file: '',
        line: 0,
        title: 'No files to review',
        description: 'All files were excluded by the configured patterns.',
        suggestion: 'Adjust exclude patterns if you want to review these files.',
        category: 'maintainability',
        confidence: 1.0
      }],
      counts: { critical: 0, high: 0, medium: 0, low: 0, info: 1, total: 1 },
      metadata: { excludedFiles, truncated: false }
    };
  }

  // Truncate if necessary
  const { truncated, wasTruncated, originalSize, truncatedSize } = truncateText(filtered, MAX_DIFF_SIZE);

  // Build prompts
  const systemPrompt = buildSystemPrompt({
    reviewType,
    focusAreas,
    projectContext
  });

  let userMessage;
  if (wasTruncated) {
    const filesIncluded = extractFilesFromDiff(truncated);
    userMessage = buildTruncatedPrompt(truncated, {
      originalSize,
      truncatedSize,
      filesIncluded
    });
  } else {
    userMessage = buildUserMessage(truncated);
  }

  // Call Z AI with retry logic
  console.log(`Calling Z AI API with model ${model}...`);
  const response = await retryWithBackoff(
    () => callZaiApi(apiKey, model, systemPrompt, userMessage),
    { maxRetries: 3, initialDelay: 2000 }
  );

  // Parse response
  const rawFindings = parseResponse(response);

  // Process findings
  const diffLines = filtered.split('\n');
  const { findings, counts } = processFindings(rawFindings, {
    threshold: severityThreshold,
    maxComments,
    diffLines
  });

  return {
    findings,
    counts,
    metadata: {
      excludedFiles,
      truncated: wasTruncated,
      originalSize,
      truncatedSize: wasTruncated ? truncatedSize : originalSize,
      model
    }
  };
}

/**
 * Check if findings contain severity at or above threshold
 */
function hasSeverityAtOrAbove(findings, threshold) {
  const severityOrder = { 'CRITICAL': 5, 'HIGH': 4, 'MEDIUM': 3, 'LOW': 2, 'INFO': 1 };
  const thresholdLevel = severityOrder[threshold.toUpperCase()] || 0;

  return findings.some(f => {
    const findingLevel = severityOrder[f.severity.toUpperCase()] || 0;
    return findingLevel >= thresholdLevel;
  });
}

module.exports = {
  performReview,
  callZaiApi,
  filterDiffByPatterns,
  hasSeverityAtOrAbove,
  ZAI_API_URL,
  DEFAULT_MODEL,
  MAX_DIFF_SIZE
};
