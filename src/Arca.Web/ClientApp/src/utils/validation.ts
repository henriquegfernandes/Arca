export function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function normalizeStoreCode(value: string): string {
  return value
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9-]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function normalizeAttributeCode(value: string): string {
  return value
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9-_]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function addRequired(
  errors: Record<string, string>,
  key: string,
  value: string,
  message: string
) {
  if (!value.trim()) errors[key] = message;
}

export function addPattern(
  errors: Record<string, string>,
  key: string,
  value: string,
  pattern: RegExp,
  message: string
) {
  if (value.trim() && !pattern.test(value.trim())) errors[key] = message;
}

export function addEmail(
  errors: Record<string, string>,
  key: string,
  value: string
) {
  addPattern(
    errors,
    key,
    value,
    /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
    "Enter a valid email."
  );
}

export function addUuid(
  errors: Record<string, string>,
  key: string,
  value: string
) {
  addPattern(
    errors,
    key,
    value,
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    "Enter a valid UUID."
  );
}
