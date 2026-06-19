import type { Category } from "../types";

export type CategoryOption = {
  id: string;
  label: string;
  depth: number;
};

export function buildCategoryOptions(
  categories: Category[],
  excludedCategoryId?: string
): CategoryOption[] {
  const activeCategories = categories.filter((category) => category.isActive);
  const childrenByParent = new Map<string | null, Category[]>();
  for (const category of activeCategories) {
    const key = category.parentCategoryId ?? null;
    childrenByParent.set(key, [...(childrenByParent.get(key) ?? []), category]);
  }

  for (const children of childrenByParent.values()) {
    children.sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
  }

  const excludedIds = excludedCategoryId
    ? new Set([excludedCategoryId, ...getDescendantIds(activeCategories, excludedCategoryId)])
    : new Set<string>();

  const options: CategoryOption[] = [];
  const visit = (category: Category, path: string[], depth: number) => {
    if (excludedIds.has(category.id)) return;

    const nextPath = [...path, category.name];
    options.push({ id: category.id, label: nextPath.join(" > "), depth });
    for (const child of childrenByParent.get(category.id) ?? []) {
      visit(child, nextPath, depth + 1);
    }
  };

  for (const root of childrenByParent.get(null) ?? []) {
    visit(root, [], 0);
  }

  return options;
}

export function getCategoryPathLabel(categories: Category[], categoryId: string | null | undefined) {
  if (!categoryId) return "-";
  const categoryById = new Map(categories.map((category) => [category.id, category]));
  const path: string[] = [];
  let current = categoryById.get(categoryId);
  const visited = new Set<string>();

  while (current && !visited.has(current.id)) {
    visited.add(current.id);
    path.unshift(current.name);
    current = current.parentCategoryId ? categoryById.get(current.parentCategoryId) : undefined;
  }

  return path.length > 0 ? path.join(" > ") : "-";
}

export function getCategoryDepth(categories: Category[], category: Category) {
  const categoryById = new Map(categories.map((item) => [item.id, item]));
  let depth = 0;
  let current = category.parentCategoryId ? categoryById.get(category.parentCategoryId) : undefined;
  const visited = new Set<string>();

  while (current && !visited.has(current.id)) {
    visited.add(current.id);
    depth += 1;
    current = current.parentCategoryId ? categoryById.get(current.parentCategoryId) : undefined;
  }

  return depth;
}

function getDescendantIds(categories: Category[], categoryId: string) {
  const childrenByParent = new Map<string, Category[]>();
  for (const category of categories) {
    if (!category.parentCategoryId) continue;
    childrenByParent.set(category.parentCategoryId, [...(childrenByParent.get(category.parentCategoryId) ?? []), category]);
  }

  const result: string[] = [];
  const visit = (id: string) => {
    for (const child of childrenByParent.get(id) ?? []) {
      result.push(child.id);
      visit(child.id);
    }
  };

  visit(categoryId);
  return result;
}
