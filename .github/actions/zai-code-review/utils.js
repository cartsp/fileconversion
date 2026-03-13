/**
 * Utility functions for Z AI Code Review Action
 */

/**
 * Sleep for a specified duration
 */
function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Retry a function with exponential backoff
 */
async function retryWithBackoff(fn, options = {}) {
  const {
    maxRetries = 3,
    initialDelay = 1000,
    maxDelay = 10000,
    backoffFactor = 2,
    retryableErrors = ['ECONNRESET', 'ETIMEDOUT', 'ENOTFOUND', '429', '503', '502', '500']
  } = options;

  let lastError;
  let delay = initialDelay;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;
      const errorMessage = error.message || String(error);
      const statusCode = error.status || error.statusCode || '';

      // Check if error is retryable
      const isRetryable = retryableErrors.some(re =>
        errorMessage.includes(re) || String(statusCode).includes(re)
      );

      if (!isRetryable || attempt === maxRetries) {
        throw error;
      }

      console.log(`Attempt ${attempt + 1} failed: ${errorMessage}. Retrying in ${delay}ms...`);
      await sleep(delay);
      delay = Math.min(delay * backoffFactor, maxDelay);
    }
  }

  throw lastError;
}

/**
 * Parse glob patterns into an array
 */
function parsePatterns(patternsString) {
  if (!patternsString) return [];
  return patternsString
    .split(',')
    .map(p => p.trim())
    .filter(p => p.length > 0);
}

/**
 * Check if a file path matches any of the exclude patterns
 */
function shouldExcludeFile(filePath, patterns) {
  if (!patterns || patterns.length === 0) return false;

  for (const pattern of patterns) {
    // Simple glob matching
    const regexPattern = pattern
      .replace(/\*\*/g, '.*')
      .replace(/\*/g, '[^/]*')
      .replace(/\?/g, '[^/]')
      .replace(/\./g, '\\.');

    const regex = new RegExp(`^${regexPattern}$`);
    if (regex.test(filePath)) {
      return true;
    }
  }

  return false;
}

/**
 * Truncate text to a maximum size, preserving structure
 */
function truncateText(text, maxSize = 50000) {
  if (text.length <= maxSize) {
    return { truncated: text, wasTruncated: false };
  }

  // Try to truncate at a file boundary
  const cutoff = text.lastIndexOf('\ndiff --git ', maxSize);
  if (cutoff > maxSize * 0.5) {
    return {
      truncated: text.substring(0, cutoff),
      wasTruncated: true,
      originalSize: text.length,
      truncatedSize: cutoff
    };
  }

  // Fall back to hard truncation
  return {
    truncated: text.substring(0, maxSize) + '\n\n... [TRUNCATED]',
    wasTruncated: true,
    originalSize: text.length,
    truncatedSize: maxSize
  };
}

/**
 * Extract file names from a diff
 */
function extractFilesFromDiff(diff) {
  const files = [];
  const regex = /diff --git a\/(.+?) b\/(.+?)/g;
  let match;

  while ((match = regex.exec(diff)) !== null) {
    const newFile = match[2];
    if (!files.includes(newFile)) {
      files.push(newFile);
    }
  }

  return files;
}

/**
 * Severity level ordering for comparison
 */
const SEVERITY_ORDER = {
  'CRITICAL': 5,
  'HIGH': 4,
  'MEDIUM': 3,
  'LOW': 2,
  'INFO': 1
};

/**
 * Check if a severity meets the threshold
 */
function severityMeetsThreshold(severity, threshold) {
  const sevLevel = SEVERITY_ORDER[severity.toUpperCase()] || 0;
  const thresholdLevel = SEVERITY_ORDER[threshold.toUpperCase()] || 0;
  return sevLevel >= thresholdLevel;
}

/**
 * Get emoji for severity level
 */
function getSeverityEmoji(severity) {
  const emojis = {
    'CRITICAL': '🚨',
    'HIGH': '🔴',
    'MEDIUM': '🟠',
    'LOW': '🟡',
    'INFO': 'ℹ️'
  };
  return emojis[severity.toUpperCase()] || '📝';
}

/**
 * Format a date for display
 */
function formatDate(date = new Date()) {
  return date.toISOString().replace('T', ' ').substring(0, 19);
}

/**
 * Sanitize text for markdown
 */
function sanitizeMarkdown(text) {
  if (!text) return '';
  return text
    .replace(/```/g, '\\`\\`\\`')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/**
 * Create a code block with language hint
 */
function createCodeBlock(code, language = '') {
  return `\`\`\`${language}\n${code}\n\`\`\``;
}

/**
 * Validate and normalize a finding object
 */
function normalizeFinding(finding) {
  return {
    severity: (finding.severity || 'INFO').toUpperCase(),
    file: finding.file || '',
    line: parseInt(finding.line, 10) || 0,
    title: String(finding.title || 'Issue found').substring(0, 80),
    description: finding.description || '',
    suggestion: finding.suggestion || '',
    category: finding.category || 'correctness',
    confidence: Math.min(1, Math.max(0, parseFloat(finding.confidence) || 0.5))
  };
}

module.exports = {
  sleep,
  retryWithBackoff,
  parsePatterns,
  shouldExcludeFile,
  truncateText,
  extractFilesFromDiff,
  SEVERITY_ORDER,
  severityMeetsThreshold,
  getSeverityEmoji,
  formatDate,
  sanitizeMarkdown,
  createCodeBlock,
  normalizeFinding
};
