import fs from 'node:fs';

export function analyzeRows(rows) {
const median = values => {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
};
const stablePhase = phase => rows.filter(row => row.phase === phase).slice(2);
const results = [];
function check(name, observations, predicate) {
  results.push({ name, observations: observations.length, result: observations.length < 3 ? 'inconclusive' : observations.every(predicate) ? 'pass' : 'fail' });
}
for (const phase of ['minimized', 'game-focus-fixture']) {
  const samples = stablePhase(phase);
  check(phase + ': media position paused', samples, row => row.mediaReady && Math.abs(row.mediaAdvanceMs) < 1);
  check(phase + ': cloud animations stopped', samples, row => !row.animating);
}
const foreground = stablePhase('foreground').filter(row => row.active && !row.minimized && row.mediaReady);
check('foreground: video and animation active', foreground, row => row.animating && Math.abs(row.mediaAdvanceMs) > 50);
const returned = stablePhase('return').filter(row => row.active && !row.minimized && row.mediaReady);
check('return: video and animation resumed', returned, row => row.animating && Math.abs(row.mediaAdvanceMs) > 50);
const phases = [...new Set(rows.map(row => row.phase))].map(phase => {
  const all = stablePhase(phase);
  const samples = ['foreground', 'foreground-resumed', 'return'].includes(phase) ? all.filter(row => row.active && !row.minimized) : all;
  return { phase, sampleCount: samples.length, activeSamples: samples.filter(row => row.active && !row.minimized).length, medianWorkingSetMiB: samples.length ? median(samples.map(row => row.workingSetMiB)) : null, medianPrivateMiB: samples.length ? median(samples.map(row => row.privateMiB)) : null, medianCpuMachinePercent: samples.length ? median(samples.map(row => row.cpuMachinePercent)) : null };
});
const startupField = rows.some(row => 'startupUiLoadedMs' in row) ? 'startupUiLoadedMs' : 'startupContentRenderedMs';
return { sampleCount: rows.length, startupEvent: startupField, startupMs: rows.find(row => row[startupField] > 0)?.[startupField], firstMediaReadySampleSeconds: rows.find(row => row.mediaReady)?.seconds, logicalProcessors: rows[0]?.logicalProcessors, mediaErrors: rows.filter(row => row.mediaError).map(row => row.mediaError), gpuUtilization: null, gpuNote: 'Windows GPU Engine performance-counter object unavailable. Rendering tier is not a utilization measurement.', renderingTier: rows[0]?.renderingTier, checks: results, phases, limitations: ['One short run, not a statistical benchmark.', 'CPU normalized over reported logical processors; two transition samples excluded per phase.', 'Focus fixture is another window in this probe, not a real game or session-tracker test.', 'Media-position and animation-clock checks do not prove present rate or decoder shutdown.', 'No physical-controller or Core-contract coverage.'] };
}

if (typeof process !== 'undefined' && process.argv[1]?.endsWith('analyze.mjs')) {
  const input = process.argv[2];
  if (!input) throw new Error('Usage: node analyze.mjs <samples.jsonl> [summary.json]');
  const rows = fs.readFileSync(input, 'utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse);
  const summary = analyzeRows(rows);
  if (process.argv[3]) fs.writeFileSync(process.argv[3], JSON.stringify(summary, null, 2) + '\n');
  process.stdout.write(JSON.stringify(summary, null, 2) + '\n');
  if (summary.checks.some(result => result.result === 'fail') || summary.mediaErrors.length) process.exitCode = 1;
}
