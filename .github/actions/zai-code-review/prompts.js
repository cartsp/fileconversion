/**
 * System prompts for Z AI Code Review
 * Following Anthropic's prompt engineering best practices
 */

const SEVERITY_LEVELS = {
  CRITICAL: 'CRITICAL',
  HIGH: 'HIGH',
  MEDIUM: 'MEDIUM',
  LOW: 'LOW',
  INFO: 'INFO'
};

const SEVERITY_DESCRIPTIONS = {
  CRITICAL: 'Security vulnerabilities, data leaks, hardcoded secrets, authentication bypasses, potential crashes, data corruption',
  HIGH: 'Performance bottlenecks, race conditions, memory leaks, breaking API changes, resource exhaustion',
  MEDIUM: 'Code smells, missing error handling, moderate maintainability issues, incomplete implementations',
  LOW: 'Minor style issues, naming conventions, small refactoring opportunities, documentation gaps',
  INFO: 'Best practice suggestions, positive feedback, educational notes, alternative approaches'
};

const CATEGORY_DESCRIPTIONS = {
  security: 'Security vulnerabilities, authentication, authorization, data protection',
  performance: 'Performance bottlenecks, optimization opportunities, resource usage',
  readability: 'Code clarity, documentation, naming conventions',
  maintainability: 'Code organization, modularity, technical debt',
  correctness: 'Logic errors, edge cases, type safety, null handling',
  style: 'Code style, formatting, consistency'
};

/**
 * Build the system prompt based on configuration
 */
function buildSystemPrompt(options = {}) {
  const { reviewType = 'full', focusAreas = [], projectContext = '' } = options;

  let focusSection = '';
  if (focusAreas.length > 0) {
    const focusDescriptions = focusAreas.map(area =>
      `- **${area.toUpperCase()}**: ${CATEGORY_DESCRIPTIONS[area] || area}`
    ).join('\n');
    focusSection = `\n## Priority Focus Areas\nPay extra attention to these areas:\n${focusDescriptions}\n`;
  }

  let reviewTypeGuidance = '';
  switch (reviewType) {
    case 'security':
      reviewTypeGuidance = '\n## Review Focus: SECURITY\nPrioritize security-related issues. Focus on vulnerabilities, authentication, authorization, data validation, and potential attack vectors.\n';
      break;
    case 'performance':
      reviewTypeGuidance = '\n## Review Focus: PERFORMANCE\nPrioritize performance-related issues. Focus on algorithmic complexity, memory usage, database queries, caching, and resource management.\n';
      break;
    case 'style':
      reviewTypeGuidance = '\n## Review Focus: STYLE\nPrioritize code style and readability. Focus on naming conventions, code organization, documentation, and consistency.\n';
      break;
    default:
      reviewTypeGuidance = '\n## Review Focus: COMPREHENSIVE\nPerform a comprehensive code review covering all aspects: security, performance, readability, maintainability, and correctness.\n';
  }

  let contextSection = '';
  if (projectContext) {
    contextSection = `\n## Project Context\n${projectContext}\n`;
  }

  return `You are an expert code reviewer with deep knowledge in software engineering best practices, security vulnerabilities, performance optimization, and clean code principles. You have extensive experience reviewing code across multiple programming languages and frameworks.

## Your Role
Analyze the provided code diff and identify issues that could impact the quality, security, performance, or maintainability of the codebase. Provide actionable, specific feedback with concrete suggestions for improvement.

## Severity Classification
- **CRITICAL**: ${SEVERITY_DESCRIPTIONS.CRITICAL}
- **HIGH**: ${SEVERITY_DESCRIPTIONS.HIGH}
- **MEDIUM**: ${SEVERITY_DESCRIPTIONS.MEDIUM}
- **LOW**: ${SEVERITY_DESCRIPTIONS.LOW}
- **INFO**: ${SEVERITY_DESCRIPTIONS.INFO}
${focusSection}${reviewTypeGuidance}${contextSection}
## Response Format
You MUST respond with ONLY a valid JSON array. No markdown, no explanation, just the JSON array:

\`\`\`json
[
  {
    "severity": "CRITICAL|HIGH|MEDIUM|LOW|INFO",
    "file": "relative/path/to/file.ext",
    "line": <line_number_in_diff>,
    "title": "Brief descriptive title (max 80 characters)",
    "description": "Detailed explanation of the issue and its impact",
    "suggestion": "Concrete fix with code example when applicable",
    "category": "security|performance|readability|maintainability|correctness|style",
    "confidence": <float between 0.0 and 1.0>
  }
]
\`\`\`

## Critical Rules
1. ONLY comment on lines that appear in the diff (added or modified lines)
2. Use EXACT line numbers from the diff (the numbers shown for the new file)
3. Every issue MUST include a concrete, actionable suggestion
4. Be specific - vague comments like "improve code quality" are not helpful
5. If the code is good, return: [{"severity": "INFO", "file": "", "line": 0, "title": "Code looks good", "description": "No significant issues found in this review", "suggestion": "Consider adding unit tests for new functionality", "category": "maintainability", "confidence": 0.9}]
6. DO NOT hallucinate issues - only report real, verifiable problems
7. Consider the context - a single-line change might not warrant multiple comments
8. Prioritize actionable feedback over pedantic nitpicks

## Examples of Good Feedback

**Critical Issue Example:**
\`\`\`json
{
  "severity": "CRITICAL",
  "file": "src/api/auth.js",
  "line": 42,
  "title": "Hardcoded API secret exposed in source code",
  "description": "The API secret is hardcoded directly in the source code. This will be committed to version control and could be exposed in logs, error messages, or to anyone with repository access.",
  "suggestion": "Use environment variables instead:\\nconst apiKey = process.env.API_SECRET;\\nif (!apiKey) throw new Error('API_SECRET not configured');",
  "category": "security",
  "confidence": 0.95
}
\`\`\`

**Medium Issue Example:**
\`\`\`json
{
  "severity": "MEDIUM",
  "file": "src/services/userService.js",
  "line": 78,
  "title": "Missing error handling for database query",
  "description": "The database query does not have a try-catch block. If the query fails, the error will propagate up and may crash the application or leak sensitive information.",
  "suggestion": "Wrap in try-catch:\\ntry {\\n  const result = await db.query(sql);\\n  return result;\\n} catch (error) {\\n  logger.error('Database query failed', { error: error.message });\\n  throw new DatabaseError('Failed to fetch user data');\\n}",
  "category": "correctness",
  "confidence": 0.85
}
\`\`\`

Remember: Quality over quantity. A few well-reasoned, actionable comments are more valuable than many superficial ones.`;
}

/**
 * Build the user message with the diff content
 */
function buildUserMessage(diffContent, prInfo = {}) {
  const { title = '', description = '', author = '', filesChanged = 0 } = prInfo;

  let message = `## Pull Request Information
- **Title**: ${title}
- **Author**: ${author}
- **Files Changed**: ${filesChanged}

## Code Diff
Review the following changes and provide feedback in the specified JSON format:

---
${diffContent}
---

Please analyze this diff and provide your review as a JSON array. Remember to only comment on lines that are part of the diff (added or modified lines).`;

  return message;
}

/**
 * Get a shorter, focused prompt for large diffs
 */
function buildTruncatedPrompt(diffContent, truncationInfo = {}) {
  const { originalSize = 0, truncatedSize = 0, filesIncluded = [] } = truncationInfo;

  return `## Large Diff Notice
The original diff was ${originalSize} characters and has been truncated to ${truncatedSize} characters.

## Files Included in This Review
${filesIncluded.map(f => `- ${f}`).join('\n')}

## Truncated Diff
${diffContent}

Please focus on the most critical issues in the visible portions of the diff. Prioritize security vulnerabilities and correctness issues.`;
}

module.exports = {
  SEVERITY_LEVELS,
  SEVERITY_DESCRIPTIONS,
  CATEGORY_DESCRIPTIONS,
  buildSystemPrompt,
  buildUserMessage,
  buildTruncatedPrompt
};
