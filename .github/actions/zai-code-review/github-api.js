/**
 * GitHub API helpers for Z AI Code Review
 */

const { getSeverityEmoji, createCodeBlock, sleep } = require('./utils');

/**
 * Fetch the PR diff from GitHub
 */
async function getPRDiff(octokit, owner, repo, pullNumber) {
  const response = await octokit.rest.pulls.get({
    owner,
    repo,
    pull_number: pullNumber,
    mediaType: {
      format: 'diff'
    }
  });

  return response.data;
}

/**
 * Get PR metadata
 */
async function getPRInfo(octokit, owner, repo, pullNumber) {
  const { data: pr } = await octokit.rest.pulls.get({
    owner,
    repo,
    pull_number: pullNumber
  });

  return {
    title: pr.title,
    description: pr.body || '',
    author: pr.user?.login || 'unknown',
    headSha: pr.head?.sha,
    baseSha: pr.base?.sha,
    filesChanged: pr.changed_files || 0,
    additions: pr.additions || 0,
    deletions: pr.deletions || 0
  };
}

/**
 * Format a review comment body
 */
function formatCommentBody(finding) {
  const emoji = getSeverityEmoji(finding.severity);
  const severityLabel = finding.severity;

  let body = `### ${emoji} **${severityLabel}**: ${finding.title}\n\n`;

  if (finding.description) {
    body += `**Issue:** ${finding.description}\n\n`;
  }

  if (finding.suggestion) {
    // Check if suggestion contains code
    if (finding.suggestion.includes('\n') || finding.suggestion.includes('(')) {
      body += `**Suggestion:**\n${createCodeBlock(finding.suggestion, finding.file?.split('.').pop() || '')}\n`;
    } else {
      body += `**Suggestion:** ${finding.suggestion}\n`;
    }
  }

  body += `\n---\n*Reviewed by Z AI Code Reviewer (GLM-5)*`;

  return body;
}

/**
 * Create a review with comments
 */
async function createReview(octokit, owner, repo, pullNumber, headSha, findings) {
  // Prepare review comments
  const comments = findings
    .filter(f => f.file && f.line > 0)
    .map(finding => ({
      path: finding.file,
      line: finding.line,
      body: formatCommentBody(finding)
    }));

  // Determine review event
  const hasBlockingIssues = findings.some(f =>
    f.severity === 'CRITICAL' || f.severity === 'HIGH'
  );

  const event = hasBlockingIssues ? 'REQUEST_CHANGES' : 'COMMENT';

  try {
    const response = await octokit.rest.pulls.createReview({
      owner,
      repo,
      pull_number: pullNumber,
      commit_id: headSha,
      body: createReviewSummary(findings),
      event,
      comments
    });

    return response.data;
  } catch (error) {
    // If batch review fails, try posting comments individually
    if (error.status === 422 || error.message.includes('path')) {
      console.log('Batch review failed, posting comments individually...');
      return await postCommentsIndividually(octokit, owner, repo, pullNumber, headSha, findings);
    }
    throw error;
  }
}

/**
 * Post comments individually when batch review fails
 */
async function postCommentsIndividually(octokit, owner, repo, pullNumber, headSha, findings) {
  const results = [];

  for (const finding of findings) {
    if (!finding.file || finding.line <= 0) continue;

    try {
      const response = await octokit.rest.pulls.createReviewComment({
        owner,
        repo,
        pull_number: pullNumber,
        commit_id: headSha,
        path: finding.file,
        line: finding.line,
        body: formatCommentBody(finding)
      });
      results.push(response.data);

      // Small delay to avoid rate limiting
      await sleep(500);
    } catch (error) {
      console.error(`Failed to post comment on ${finding.file}:${finding.line}:`, error.message);
    }
  }

  return results;
}

/**
 * Create a summary comment for the PR
 */
function createReviewSummary(findings) {
  const counts = {
    CRITICAL: 0,
    HIGH: 0,
    MEDIUM: 0,
    LOW: 0,
    INFO: 0
  };

  for (const finding of findings) {
    const sev = finding.severity.toUpperCase();
    if (counts.hasOwnProperty(sev)) {
      counts[sev]++;
    }
  }

  const total = findings.length;

  let summary = `## 🤖 Z AI Code Review Summary\n\n`;
  summary += `| Severity | Count |\n`;
  summary += `|----------|-------|\n`;
  summary += `| 🚨 Critical | ${counts.CRITICAL} |\n`;
  summary += `| 🔴 High | ${counts.HIGH} |\n`;
  summary += `| 🟠 Medium | ${counts.MEDIUM} |\n`;
  summary += `| 🟡 Low | ${counts.LOW} |\n`;
  summary += `| ℹ️ Info | ${counts.INFO} |\n`;
  summary += `| **Total** | **${total}** |\n\n`;

  if (counts.CRITICAL > 0) {
    summary += `### ⚠️ Action Required\n`;
    summary += `This PR contains **critical issues** that should be addressed before merging.\n\n`;
  } else if (counts.HIGH > 0) {
    summary += `### 📋 Recommended Actions\n`;
    summary += `This PR contains **high-severity issues** that should be reviewed carefully.\n\n`;
  } else if (total === 0) {
    summary += `### ✅ All Clear\n`;
    summary += `No significant issues found in this review.\n\n`;
  }

  // Add top issues
  const topIssues = findings
    .filter(f => f.severity === 'CRITICAL' || f.severity === 'HIGH')
    .slice(0, 5);

  if (topIssues.length > 0) {
    summary += `### 🔍 Top Issues to Address\n\n`;
    for (const issue of topIssues) {
      const emoji = getSeverityEmoji(issue.severity);
      summary += `- ${emoji} **${issue.file}${issue.line ? `:${issue.line}` : ''}** - ${issue.title}\n`;
    }
    summary += `\n`;
  }

  summary += `---\n`;
  summary += `*Powered by [Z AI](https://z.ai) with GLM-5 model*\n`;

  return summary;
}

/**
 * Post a summary comment on the PR
 */
async function postSummaryComment(octokit, owner, repo, pullNumber, findings) {
  const body = createReviewSummary(findings);

  await octokit.rest.issues.createComment({
    owner,
    repo,
    issue_number: pullNumber,
    body
  });
}

/**
 * Delete any existing bot comments (optional cleanup)
 */
async function cleanupOldComments(octokit, owner, repo, pullNumber, botIdentifier = 'Z AI Code Reviewer') {
  try {
    const { data: comments } = await octokit.rest.issues.listComments({
      owner,
      repo,
      issue_number: pullNumber
    });

    const botComments = comments.filter(c =>
      c.body?.includes(botIdentifier) || c.user?.type === 'Bot'
    );

    for (const comment of botComments) {
      try {
        await octokit.rest.issues.deleteComment({
          owner,
          repo,
          comment_id: comment.id
        });
        await sleep(200);
      } catch (e) {
        // Ignore deletion errors
      }
    }
  } catch (error) {
    console.log('Could not cleanup old comments:', error.message);
  }
}

/**
 * Get the list of changed files in a PR
 */
async function getChangedFiles(octokit, owner, repo, pullNumber) {
  const { data: files } = await octokit.rest.pulls.listFiles({
    owner,
    repo,
    pull_number: pullNumber
  });

  return files.map(f => ({
    filename: f.filename,
    status: f.status,
    additions: f.additions,
    deletions: f.deletions,
    changes: f.changes,
    patch: f.patch
  }));
}

module.exports = {
  getPRDiff,
  getPRInfo,
  formatCommentBody,
  createReview,
  postCommentsIndividually,
  createReviewSummary,
  postSummaryComment,
  cleanupOldComments,
  getChangedFiles
};
