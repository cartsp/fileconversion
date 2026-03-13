/**
 * Z AI Code Review Action - Main Entry Point
 *
 * Performs AI-powered code review on pull requests using Z AI's GLM-5 model
 */

const core = require('@actions/core');
const github = require('@actions/github');
const { performReview, hasSeverityAtOrAbove } = require('./review');
const { getPRDiff, getPRInfo, createReview, postSummaryComment } = require('./github-api');
const { parsePatterns, formatDate } = require('./utils');

/**
 * Main action entry point
 */
async function run() {
  try {
    // Get inputs
    const apiKey = core.getInput('zai-api-key', { required: true });
    const model = core.getInput('zai-model') || 'GLM-5';
    const reviewType = core.getInput('review-type') || 'full';
    const severityThreshold = core.getInput('severity-threshold') || 'low';
    const maxComments = parseInt(core.getInput('max-comments') || '25', 10);
    const failOnSeverity = core.getInput('fail-on-severity') || 'critical';
    const excludePatternsStr = core.getInput('exclude-patterns') || '';
    const focusAreasStr = core.getInput('focus-areas') || '';
    const projectContext = core.getInput('project-context') || '';
    const token = core.getInput('github-token') || process.env.GITHUB_TOKEN;
    const prNumberInput = core.getInput('pr-number');

    // Parse patterns
    const excludePatterns = parsePatterns(excludePatternsStr);
    const focusAreas = parsePatterns(focusAreasStr);

    // Log configuration
    console.log(`Z AI Code Review starting at ${formatDate()}`);
    console.log(`Model: ${model}`);
    console.log(`Review type: ${reviewType}`);
    console.log(`Severity threshold: ${severityThreshold}`);
    console.log(`Max comments: ${maxComments}`);
    console.log(`Fail on severity: ${failOnSeverity}`);
    if (excludePatterns.length > 0) {
      console.log(`Exclude patterns: ${excludePatterns.join(', ')}`);
    }
    if (focusAreas.length > 0) {
      console.log(`Focus areas: ${focusAreas.join(', ')}`);
    }

    // Get GitHub context
    const context = github.context;
    const { owner, repo } = context.repo;

    // Validate we're in a PR context or have a PR number input
    const pullNumber = context.payload.pull_request?.number || parseInt(prNumberInput, 10);

    if (!pullNumber) {
      console.log('No pull request context or pr-number input, skipping review');
      core.setOutput('issues-found', '0');
      core.setOutput('critical-count', '0');
      core.setOutput('high-count', '0');
      core.setOutput('medium-count', '0');
      core.setOutput('low-count', '0');
      core.setOutput('info-count', '0');
      return;
    }

    console.log(`Reviewing PR #${pullNumber} in ${owner}/${repo}`);

    // Initialize GitHub client
    const octokit = github.getOctokit(token);

    // Get PR information
    console.log('Fetching PR information...');
    const prInfo = await getPRInfo(octokit, owner, repo, pullNumber);
    console.log(`PR: "${prInfo.title}" by ${prInfo.author}`);
    console.log(`Changes: ${prInfo.filesChanged} files, +${prInfo.additions}/-${prInfo.deletions}`);

    // Get PR diff
    console.log('Fetching PR diff...');
    const diff = await getPRDiff(octokit, owner, repo, pullNumber);
    console.log(`Diff size: ${diff.length} characters`);

    // Perform the review
    console.log('Starting AI code review...');
    const { findings, counts, metadata } = await performReview(diff, {
      apiKey,
      model,
      reviewType,
      focusAreas,
      projectContext,
      excludePatterns,
      severityThreshold,
      maxComments
    });

    // Log results
    console.log(`Review complete: ${counts.total} findings`);
    console.log(`  Critical: ${counts.critical}`);
    console.log(`  High: ${counts.high}`);
    console.log(`  Medium: ${counts.medium}`);
    console.log(`  Low: ${counts.low}`);
    console.log(`  Info: ${counts.info}`);

    if (metadata.truncated) {
      console.log(`Warning: Diff was truncated from ${metadata.originalSize} to ${metadata.truncatedSize} characters`);
    }

    if (metadata.excludedFiles.length > 0) {
      console.log(`Excluded ${metadata.excludedFiles.length} files based on patterns`);
    }

    // Post review comments
    if (findings.length > 0) {
      console.log('Posting review comments...');
      try {
        await createReview(octokit, owner, repo, pullNumber, prInfo.headSha, findings);
        console.log('Review comments posted successfully');
      } catch (error) {
        console.error('Failed to post review:', error.message);
        // Try to post at least a summary comment
        try {
          await postSummaryComment(octokit, owner, repo, pullNumber, findings);
          console.log('Summary comment posted as fallback');
        } catch (summaryError) {
          console.error('Failed to post summary:', summaryError.message);
        }
      }
    } else {
      console.log('No findings to post');
      // Post a summary even if no issues found
      await postSummaryComment(octokit, owner, repo, pullNumber, findings);
    }

    // Set outputs
    core.setOutput('issues-found', String(counts.total));
    core.setOutput('critical-count', String(counts.critical));
    core.setOutput('high-count', String(counts.high));
    core.setOutput('medium-count', String(counts.medium));
    core.setOutput('low-count', String(counts.low));
    core.setOutput('info-count', String(counts.info));

    // Check if we should fail the workflow
    if (failOnSeverity && failOnSeverity !== 'none') {
      const shouldFail = hasSeverityAtOrAbove(findings, failOnSeverity);
      if (shouldFail) {
        const blockingFindings = findings.filter(f =>
          hasSeverityAtOrAbove([f], failOnSeverity)
        );
        core.setFailed(
          `Found ${blockingFindings.length} issue(s) at or above "${failOnSeverity}" severity. ` +
          `Please address these issues before merging.`
        );
        return;
      }
    }

    console.log('Z AI Code Review completed successfully');

  } catch (error) {
    console.error('Action failed:', error);
    core.setFailed(error.message);

    // Set safe default outputs
    core.setOutput('issues-found', '0');
    core.setOutput('critical-count', '0');
    core.setOutput('high-count', '0');
    core.setOutput('medium-count', '0');
    core.setOutput('low-count', '0');
    core.setOutput('info-count', '0');
  }
}

// Run the action
run();
