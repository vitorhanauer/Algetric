export const ENTER_2D = 60_000;
export const EXIT_2D = 90_000;
export function desiredMode(current, height) {
  if (!Number.isFinite(height) || height <= 0) return current;
  if (current === '3D' && height < ENTER_2D) return '2D';
  if (current === '2D' && height > EXIT_2D) return '3D';
  return current;
}
export function acceptState(previous, next) {
  return !previous || previous.session !== next.session || next.revision >= previous.revision;
}
