import type { Pagination } from "../types";

export function PaginationControls({
  pagination,
  onPageChange,
}: {
  pagination: Pagination | null;
  onPageChange: (page: number) => void;
}) {
  if (!pagination) return null;

  const firstItem = pagination.totalCount === 0
    ? 0
    : (pagination.page - 1) * pagination.pageSize + 1;
  const lastItem = Math.min(pagination.page * pagination.pageSize, pagination.totalCount);

  return (
    <div className="pagination-bar">
      <span>
        {firstItem}-{lastItem} of {pagination.totalCount}
      </span>
      <div className="row-actions">
        <button
          className="secondary"
          disabled={pagination.page <= 1}
          onClick={() => onPageChange(pagination.page - 1)}
        >
          Previous
        </button>
        <button
          className="secondary"
          disabled={pagination.totalPages === 0 || pagination.page >= pagination.totalPages}
          onClick={() => onPageChange(pagination.page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
}
