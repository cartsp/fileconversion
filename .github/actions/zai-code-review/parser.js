/**
 * Response parser for Z AI Code Review
 * Handles JSON extraction, validation, and severity filtering
 */

const { normalizeFinding, SEVERITY_ORDER, severityMeetsThreshold } = require('./utils');

/**
 * Extract JSON from a response that might contain markdown code blocks
 */
function extractJsonFromResponse(response) {
  if (!response) {
    throw new Error('Empty response from AI');
  }

  let jsonStr = response.trim();

  // Try to extract JSON from markdown code block
  const codeBlockMatch = jsonStr.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (codeBlockMatch) {
    jsonStr = codeBlockMatch[1].trim();
  }

  // Remove any leading/trailing text that's not JSON
  const jsonStart = jsonStr.indexOf('[');
  const jsonEnd = jsonStr.lastIndexOf(']');

  if (jsonStart !== -1 && jsonEnd !== -1 && jsonEnd > jsonStart) {
    jsonStr = jsonStr.substring(jsonStart, jsonEnd + 1);
  }

  return jsonStr;
}

/**
 * Parse the AI response into structured findings
 */
function parseResponse(response) {
  try {
    const jsonStr = extractJsonFromResponse(response);
    const parsed = JSON.parse(jsonStr);

    if (!Array.isArray(parsed)) {
      console.warn('Response is not an array, wrapping in array');
      return [normalizeFinding(parsed)];
    }

    return parsed.map(finding => normalizeFinding(finding));
  } catch (error) {
    console.error('Failed to parse JSON response:', error.message);
    console.error('Attempting fallback regex extraction...');

    // Fallback: try to extract individual findings using regex
    return fallbackExtractFindings(response);
  }
}

/**
 * Fallback extraction using regex for malformed responses
 */
function fallbackExtractFindings(response) {
  const findings = [];

  // Pattern to match finding objects
  const findingPattern = /\{\s*"severity"\s*:\s*"(CRITICAL|HIGH|MEDIUM|LOW|INFO)"[^}]*"title"\s*:\s*"([^"]+)"[^}]*\}/gi;

  let match;
  while ((match = findingPattern.exec(response)) !== null) {
    try {
      // Try to parse the matched object
      const objMatch = match[0];
      const parsed = JSON.parse(objMatch + '}');

      findings.push(normalizeFinding(parsed));
    } catch (e) {
      // Create a basic finding from captured groups
      findings.push(normalizeFinding({
        severity: match[1],
        title: match[2],
        description: 'Extracted from partially malformed response',
        suggestion: 'Please review manually',
        category: 'correctness',
        confidence: 0.5
      }));
    }
  }

  if (findings.length === 0) {
    // Return a fallback info message
    return [{
      severity: 'INFO',
      file: '',
      line: 0,
      title: 'Review completed with parsing issues',
      description: 'The AI response could not be fully parsed. Please review the changes manually.',
      suggestion: 'Consider checking the action logs for the raw AI response.',
      category: 'maintainability',
      confidence: 0.5
    }];
  }

  return findings;
}

/**
 * Validate that a finding references lines in the diff
 */
function validateFindingAgainstDiff(finding, diffLines) {
  // If no file specified, keep the finding but mark with lower confidence
  if (!finding.file) {
    return { ...finding, confidence: Math.min(finding.confidence, 0.5) };
  }

  // Check if the file exists in the diff
  const fileInDiff = diffLines.some(line =>
    line.includes(`b/${finding.file}`) || line.includes(`+++ b/${finding.file}`)
  );

  if (!fileInDiff) {
    console.warn(`Finding references file not in diff: ${finding.file}`);
    return null; // Filter out findings for files not in the diff
  }

  // If line number is specified, validate it
  if (finding.line > 0) {
    // The line should be a reasonable number (basic sanity check)
    if (finding.line > 10000) {
      console.warn(`Unreasonable line number: ${finding.line} in ${finding.file}`);
      return { ...finding, line: 0, confidence: Math.min(finding.confidence, 0.3) };
    }
  }

  return finding;
}

/**
 * Filter findings by severity threshold
 */
function filterBySeverity(findings, threshold) {
  if (!threshold) return findings;

  return findings.filter(finding =>
    severityMeetsThreshold(finding.severity, threshold)
  );
}

/**
 * Sort findings by severity (highest first)
 */
function sortBySeverity(findings) {
  return [...findings].sort((a, b) => {
    const orderA = SEVERITY_ORDER[a.severity] || 0;
    const orderB = SEVERITY_ORDER[b.severity] || 0;
    return orderB - orderA;
  });
}

/**
 * Limit the number of findings per severity level
 */
function limitFindings(findings, maxTotal = 25, maxPerSeverity = 10) {
  const bySeverity = {
    CRITICAL: [],
    HIGH: [],
    MEDIUM: [],
    LOW: [],
    INFO: []
  };

  // Group by severity
  for (const finding of findings) {
    const sev = finding.severity.toUpperCase();
    if (bySeverity[sev]) {
      bySeverity[sev].push(finding);
    }
  }

  // Limit each severity level
  const limited = [];
  for (const severity of ['CRITICAL', 'HIGH', 'MEDIUM', 'LOW', 'INFO']) {
    limited.push(...bySeverity[severity].slice(0, maxPerSeverity));
  }

  // Limit total
  return limited.slice(0, maxTotal);
}

/**
 * Count findings by severity
 */
function countBySeverity(findings) {
  const counts = {
    critical: 0,
    high: 0,
    medium: 0,
    low: 0,
    info: 0,
    total: findings.length
  };

  for (const finding of findings) {
    const sev = finding.severity.toLowerCase();
    if (counts.hasOwnProperty(sev)) {
      counts[sev]++;
    }
  }

  return counts;
}

/**
 * Deduplicate findings that might be similar
 */
function deduplicateFindings(findings) {
  const seen = new Set();
  const unique = [];

  for (const finding of findings) {
    // Create a key based on file, line, and title
    const key = `${finding.file}:${finding.line}:${finding.title}`;

    if (!seen.has(key)) {
      seen.add(key);
      unique.push(finding);
    }
  }

  return unique;
}

/**
 * Process all findings through validation, filtering, and limiting
 */
function processFindings(rawFindings, options = {}) {
  const {
    threshold = 'low',
    maxComments = 25,
    diffLines = []
  } = options;

  // Validate against diff
  let findings = rawFindings
    .map(f => validateFindingAgainstDiff(f, diffLines))
    .filter(f => f !== null);

  // Deduplicate
  findings = deduplicateFindings(findings);

  // Filter by severity
  findings = filterBySeverity(findings, threshold);

  // Sort by severity
  findings = sortBySeverity(findings);

  // Limit total findings
  findings = limitFindings(findings, maxComments);

  // Count by severity
  const counts = countBySeverity(findings);

  return { findings, counts };
}

module.exports = {
  extractJsonFromResponse,
  parseResponse,
  fallbackExtractFindings,
  validateFindingAgainstDiff,
  filterBySeverity,
  sortBySeverity,
  limitFindings,
  countBySeverity,
  deduplicateFindings,
  processFindings
};
