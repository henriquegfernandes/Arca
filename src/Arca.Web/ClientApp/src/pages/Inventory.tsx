import { useEffect, useMemo, useState } from "react";
import { ArrowDownToLine, ArrowUpFromLine, CheckCircle2, ChevronLeft, Edit3, Eye, Grid2X2, List, MoreVertical, PackagePlus, PowerOff, RefreshCw, Save, Search, SlidersHorizontal, Trash2, X } from "lucide-react";
import { ConfirmDialog, EntityModal } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { ProductImageThumb } from "../components/ProductImageThumb";
import { Products } from "./Products";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import { buildCategoryOptions } from "../utils/categories";
import type {
  Category,
  InventoryProductDetails,
  InventoryProductSummary,
  InventoryVariant,
  Pagination,
  StockLocation,
} from "../types";

type MovementType = "Entry" | "Exit" | "Adjustment";

type MovementItemDraft = {
  selected: boolean;
  stockLocationId: string;
  quantity: string;
  unitCost: string;
};

type ProductEditorState =
  | { mode: "create" }
  | { mode: "edit"; productId: string }
  | null;

export function Inventory() {
  const { currentTenant, currentStore } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const storeId = currentStore?.id ?? "";
  const [products, setProducts] = useState<InventoryProductSummary[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [stockLocations, setStockLocations] = useState<StockLocation[]>([]);
  const [pagination, setPagination] = useState<Pagination | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [categoryId, setCategoryId] = useState("");
  const [status, setStatus] = useState("");
  const [stockLocationId, setStockLocationId] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [outOfStockOnly, setOutOfStockOnly] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [viewMode, setViewMode] = useState<"grid" | "list">("grid");
  const [productEditor, setProductEditor] = useState<ProductEditorState>(null);
  const [productToDisable, setProductToDisable] = useState<InventoryProductSummary | null>(null);
  const [productToActivate, setProductToActivate] = useState<InventoryProductSummary | null>(null);
  const [productToDelete, setProductToDelete] = useState<InventoryProductSummary | null>(null);

  const [details, setDetails] = useState<InventoryProductDetails | null>(null);
  const [movementType, setMovementType] = useState<MovementType | null>(null);
  const [movementReason, setMovementReason] = useState("");
  const [movementNotes, setMovementNotes] = useState("");
  const [movementItems, setMovementItems] = useState<Record<string, MovementItemDraft>>({});

  useEffect(() => {
    if (!tenantId || !storeId) {
      setProducts([]);
      setCategories([]);
      setStockLocations([]);
      setPagination(null);
      return;
    }

    void loadSupportData();
    void loadProducts(1);
  }, [tenantId, storeId]);

  async function loadSupportData() {
    if (!tenantId || !storeId) return;
    const [categoryData, locationData] = await Promise.all([
      api.catalog.categories.list(tenantId, { pageSize: 100 }),
      api.inventory.stockLocations(tenantId, storeId),
    ]);
    setCategories(categoryData.categories ?? []);
    setStockLocations(locationData.stockLocations ?? []);
    setStockLocationId((current) => current || locationData.stockLocations?.[0]?.id || "");
  }

  async function loadProducts(nextPage = page) {
    if (!tenantId || !storeId) {
      setMessage(t("inventory.contextRequired"));
      return;
    }

    setIsLoading(true);
    setMessage(null);
    try {
      const data = await api.inventory.products(tenantId, storeId, {
        page: nextPage,
        pageSize: 24,
        search,
        categoryId: categoryId || undefined,
        status: status || undefined,
        lowStockOnly,
        outOfStockOnly,
        stockLocationId: stockLocationId || undefined,
      });
      setProducts(data.products ?? []);
      setPagination(data.pagination ?? null);
      setPage(data.pagination?.page ?? nextPage);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("inventory.loadFailed"));
    } finally {
      setIsLoading(false);
    }
  }

  async function openDetails(productId: string) {
    if (!tenantId || !storeId) return;
    setMessage(null);
    try {
      const data = await api.inventory.productDetails(tenantId, storeId, productId, stockLocationId || undefined);
      setDetails(data);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("inventory.detailsLoadFailed"));
    }
  }

  async function closeProductEditor() {
    setProductEditor(null);
    await loadProducts(page);
  }

  async function disableProduct(product: InventoryProductSummary) {
    try {
      await api.catalog.products.disable(product.productId, tenantId);
      setMessage(t("products.productDisabled"));
      setProductToDisable(null);
      await loadProducts(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productDisableFailed"));
    }
  }

  async function activateProduct(product: InventoryProductSummary) {
    try {
      await api.catalog.products.activate(product.productId, tenantId);
      setMessage(t("products.productActivated"));
      setProductToActivate(null);
      await loadProducts(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productActivateFailed"));
    }
  }

  async function deleteProduct(product: InventoryProductSummary) {
    try {
      await api.catalog.products.delete(product.productId, tenantId);
      setMessage(t("products.productDeleted"));
      setProductToDelete(null);
      await loadProducts(page);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productDeleteFailed"));
    }
  }

  function openMovement(type: MovementType) {
    if (!details) return;
    const initialLocation = stockLocationId || stockLocations[0]?.id || "";
    setMovementType(type);
    setMovementReason(type === "Entry" ? "Purchase" : type === "Exit" ? "Sale" : "Adjustment");
    setMovementNotes("");
    setMovementItems(Object.fromEntries(details.variants.map((variant) => [
      variant.productVariantId,
      {
        selected: false,
        stockLocationId: initialLocation,
        quantity: type === "Adjustment" ? String(variant.quantity) : "1",
        unitCost: "",
      },
    ])));
  }

  async function submitMovement() {
    if (!details || !movementType || !tenantId || !storeId) return;

    const items = details.variants
      .map((variant) => ({ variant, draft: movementItems[variant.productVariantId] }))
      .filter((item) => item.draft?.selected)
      .map(({ variant, draft }) => ({
        productVariantId: variant.productVariantId,
        stockLocationId: draft.stockLocationId,
        quantity: parseInt(draft.quantity) || 0,
        unitCost: draft.unitCost ? parseFloat(draft.unitCost) : null,
      }));

    if (items.length === 0) {
      setMessage(t("inventory.selectAtLeastOneVariant"));
      return;
    }

    const invalidExit = movementType === "Exit" && items.some((item) => {
      const variant = details.variants.find((candidate) => candidate.productVariantId === item.productVariantId);
      return variant && item.quantity > variant.availableQuantity;
    });
    if (invalidExit) {
      setMessage(t("inventory.exitGreaterThanAvailable"));
      return;
    }

    try {
      await api.inventory.movement({
        type: movementType,
        tenantId,
        storeId,
        stockLocationId: stockLocationId || stockLocations[0]?.id,
        items,
        reason: movementReason.trim() || null,
        notes: movementNotes.trim() || null,
      });
      setMessage(t("inventory.movementSavedSuccessfully"));
      setMovementType(null);
      await loadProducts(page);
      await openDetails(details.productId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("inventory.movementSaveFailed"));
    }
  }

  const selectedCount = useMemo(
    () => Object.values(movementItems).filter((item) => item.selected).length,
    [movementItems]
  );

  const isActionMode = movementType !== null;

  return (
    <section className="stores-panel">
      <div className="panel-section inventory-filter-panel">
        <div className="inventory-toolbar">
          <div className="view-toggle" aria-label={t("common.viewMode")}>
            <button
              className={viewMode === "grid" ? "secondary active" : "secondary"}
              type="button"
              onClick={() => setViewMode("grid")}
            >
              <Grid2X2 size={16} />
              {t("common.grid")}
            </button>
            <button
              className={viewMode === "list" ? "secondary active" : "secondary"}
              type="button"
              onClick={() => setViewMode("list")}
            >
              <List size={16} />
              {t("common.list")}
            </button>
          </div>
          <button className="primary" type="button" onClick={() => setProductEditor({ mode: "create" })}>
            <PackagePlus size={16} />
            {t("products.addNew")}
          </button>
        </div>

        <form
          className="inventory-search-row"
          onSubmit={(event) => {
            event.preventDefault();
            void loadProducts(1);
          }}
        >
          <label className="search-input">
            <span>{t("inventory.searchPlaceholder")}</span>
            <input value={search} onChange={(event) => setSearch(event.target.value)} />
          </label>
          <button className="secondary" type="submit" disabled={isLoading}>
            <Search size={16} />
            {isLoading ? t("common.loading") : t("common.search")}
          </button>
          <button className="secondary" type="button" onClick={() => loadProducts(page)} disabled={isLoading}>
            <RefreshCw size={16} />
            {t("common.refresh")}
          </button>
        </form>

        <div className="inventory-filter-row">
          <label className="field">
            <span>{t("products.category")}</span>
            <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">{t("inventory.allCategories")}</option>
              {buildCategoryOptions(categories).map((category) => (
                <option key={category.id} value={category.id}>{category.label}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t("common.status")}</span>
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="">{t("inventory.allStatuses")}</option>
              <option value="Active">{t("common.active")}</option>
              <option value="Draft">{t("common.draft")}</option>
              <option value="Inactive">{t("common.inactive")}</option>
            </select>
          </label>
          <label className="field">
            <span>{t("inventory.stockLocation")}</span>
            <select value={stockLocationId} onChange={(event) => setStockLocationId(event.target.value)}>
              <option value="">{t("inventory.allLocations")}</option>
              {stockLocations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
            </select>
          </label>
          <div className="inventory-checks">
            <label>
              <input type="checkbox" checked={lowStockOnly} onChange={(event) => setLowStockOnly(event.target.checked)} />
              <span>{t("inventory.lowStockOnly")}</span>
            </label>
            <label>
              <input type="checkbox" checked={outOfStockOnly} onChange={(event) => setOutOfStockOnly(event.target.checked)} />
              <span>{t("inventory.outOfStockOnly")}</span>
            </label>
          </div>
          <button className="secondary" type="button" onClick={() => loadProducts(1)}>
            {t("common.applyFilters")}
          </button>
        </div>
        {message && <div className={message === t("inventory.movementSavedSuccessfully") ? "notice success" : "notice error"}>{message}</div>}
      </div>

      {viewMode === "grid" ? (
      <div className="inventory-grid">
        {products.length === 0 ? (
          <div className="empty-state">{t("inventory.noProductsFound")}</div>
        ) : products.map((product) => (
          <article key={product.productId} className="inventory-card">
            <div className="inventory-card-header">
              <ProductImageThumb src={product.mainImageUrl} alt={product.name} size="md" />
              <div className="inventory-card-title">
                <strong>{product.name}</strong>
                <span>{product.baseSku}</span>
                <span>{product.categoryName ?? t("inventory.noCategory")}</span>
              </div>
            </div>
            <div className="badge-row">
              <span className="inventory-badge">{product.status}</span>
              {product.hasLowStock && <span className="inventory-badge warn">{t("inventory.lowStock")}</span>}
              {product.isOutOfStock && <span className="inventory-badge danger">{t("inventory.outOfStock")}</span>}
              {product.totalReservedQuantity > 0 && <span className="inventory-badge warn">{t("inventory.reservedQuantity")}</span>}
            </div>
            <div className="inventory-stats">
              <div className="inventory-stat"><span>{t("inventory.totalQuantity")}</span><strong>{product.totalQuantity}</strong></div>
              <div className="inventory-stat"><span>{t("inventory.availableQuantity")}</span><strong>{product.totalAvailableQuantity}</strong></div>
              <div className="inventory-stat"><span>{t("inventory.variantCount")}</span><strong>{product.variantCount}</strong></div>
            </div>
            <div className="inventory-card-actions">
              <button className="primary" type="button" onClick={() => openDetails(product.productId)}><Eye size={16} />{t("inventory.viewDetails")}</button>
              <ProductActionMenu
                product={product}
                onEdit={() => setProductEditor({ mode: "edit", productId: product.productId })}
                onActivate={() => setProductToActivate(product)}
                onDisable={() => setProductToDisable(product)}
                onDelete={() => setProductToDelete(product)}
              />
            </div>
          </article>
        ))}
      </div>
      ) : (
        <div className="table-shell inventory-list">
          <table>
            <thead>
              <tr>
                <th>{t("products.name")}</th>
                <th>{t("products.sku")}</th>
                <th>{t("products.category")}</th>
                <th>{t("inventory.totalQuantity")}</th>
                <th>{t("inventory.availableQuantity")}</th>
                <th>{t("inventory.variantCount")}</th>
                <th>{t("common.status")}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {products.length === 0 ? (
                <tr><td colSpan={8}>{t("inventory.noProductsFound")}</td></tr>
              ) : products.map((product) => (
                <tr key={product.productId}>
                  <td>
                    <div className="product-cell">
                      <ProductImageThumb src={product.mainImageUrl} alt={product.name} size="sm" />
                      <strong>{product.name}</strong>
                    </div>
                  </td>
                  <td>{product.baseSku}</td>
                  <td>{product.categoryName ?? t("inventory.noCategory")}</td>
                  <td>{product.totalQuantity}</td>
                  <td>{product.totalAvailableQuantity}</td>
                  <td>{product.variantCount}</td>
                  <td>
                    <div className="badge-row">
                      <span className="inventory-badge">{product.status}</span>
                      {product.hasLowStock && <span className="inventory-badge warn">{t("inventory.lowStockShort")}</span>}
                      {product.isOutOfStock && <span className="inventory-badge danger">{t("inventory.outOfStockShort")}</span>}
                    </div>
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="primary" type="button" onClick={() => openDetails(product.productId)}><Eye size={16} />{t("inventory.viewDetails")}</button>
                      <ProductActionMenu
                        product={product}
                        onEdit={() => setProductEditor({ mode: "edit", productId: product.productId })}
                        onActivate={() => setProductToActivate(product)}
                        onDisable={() => setProductToDisable(product)}
                        onDelete={() => setProductToDelete(product)}
                      />
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <PaginationControls pagination={pagination} onPageChange={loadProducts} />

      {details && (
        <EntityModal
          title={t("inventory.detailsTitle")}
          size="wide"
          onClose={() => { setDetails(null); setMovementType(null); }}
          footer={isActionMode ? (
            <>
              <button className="secondary" type="button" onClick={() => setMovementType(null)}><ChevronLeft size={16} />{t("common.back")}</button>
              <button className="primary" type="button" onClick={submitMovement}>
                <Save size={16} />
                {t("inventory.saveMovement")} ({selectedCount})
              </button>
            </>
          ) : (
            <button className="secondary" type="button" onClick={() => { setDetails(null); setMovementType(null); }}><X size={16} />{t("common.close")}</button>
          )}
        >
          <div className="inventory-detail-layout">
            <div className="inventory-detail-header">
              <ProductImageThumb src={details.mainImageUrl} alt={details.name} size="lg" />
              <div className="inventory-card-title">
                <strong>{details.name}</strong>
                <span>{details.baseSku}</span>
                <span>{details.categoryName ?? t("inventory.noCategory")}</span>
                {details.description && <span>{details.description}</span>}
              </div>
            </div>

            <div className="inventory-stats">
              <div className="inventory-stat"><span>{t("inventory.totalQuantity")}</span><strong>{details.totalQuantity}</strong></div>
              <div className="inventory-stat"><span>{t("inventory.reservedQuantity")}</span><strong>{details.totalReservedQuantity}</strong></div>
              <div className="inventory-stat"><span>{t("inventory.availableQuantity")}</span><strong>{details.totalAvailableQuantity}</strong></div>
              <div className="inventory-stat"><span>{t("inventory.variantCount")}</span><strong>{details.variants.length}</strong></div>
            </div>

            {!movementType && (
              <>
                <div className="table-shell inventory-variant-table">
                  <table>
                    <thead>
                      <tr>
                        <th>{t("inventory.variant")}</th>
                        <th>{t("products.sku")}</th>
                        <th>{t("inventory.quantity")}</th>
                        <th>{t("inventory.reservedQuantity")}</th>
                        <th>{t("inventory.availableQuantity")}</th>
                        <th>{t("inventory.minimumStock")}</th>
                        <th>{t("common.status")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {details.variants.map((variant) => (
                        <tr key={variant.productVariantId}>
                          <td>
                            <VariantLabel productName={details.name} variant={variant} />
                          </td>
                          <td>{variant.sku}<br /><small>{variant.barcode ?? ""}</small></td>
                          <td>{variant.quantity}</td>
                          <td>{variant.reservedQuantity}</td>
                          <td>{variant.availableQuantity}</td>
                          <td>{variant.minimumStock}</td>
                          <td>
                            <div className="badge-row">
                              <span className="inventory-badge">{variant.status}</span>
                              {variant.isLowStock && <span className="inventory-badge warn">{t("inventory.lowStockShort")}</span>}
                              {variant.isOutOfStock && <span className="inventory-badge danger">{t("inventory.outOfStockShort")}</span>}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="inventory-detail-actions">
                  <button className="primary" type="button" onClick={() => openMovement("Entry")}><ArrowDownToLine size={16} />{t("inventory.registerEntry")}</button>
                  <button className="secondary" type="button" onClick={() => openMovement("Exit")}><ArrowUpFromLine size={16} />{t("inventory.registerExit")}</button>
                  <button className="secondary" type="button" onClick={() => openMovement("Adjustment")}><SlidersHorizontal size={16} />{t("inventory.registerAdjustment")}</button>
                </div>
              </>
            )}

            {movementType && (
              <div className="inventory-action-panel">
                <div className="section-heading">
                  <div>
                    <h2>{movementTitle(movementType, t)}</h2>
                    <p>{movementType === "Adjustment" ? t("inventory.adjustmentFinalQuantityHelp") : t("inventory.selectVariants")}</p>
                  </div>
                  <button className="secondary" type="button" onClick={() => setMovementType(null)}><ChevronLeft size={16} />{t("common.back")}</button>
                </div>
              <div className="movement-items">
                {details.variants.map((variant) => (
                  <MovementVariantRow
                    key={variant.productVariantId}
                    type={movementType}
                    productName={details.name}
                    variant={variant}
                    locations={stockLocations}
                    value={movementItems[variant.productVariantId]}
                    onChange={(value) => setMovementItems((current) => ({ ...current, [variant.productVariantId]: value }))}
                  />
                ))}
              </div>
              <div className="form-grid" style={{ marginTop: "1rem" }}>
                <Field label={t("inventory.reason")} value={movementReason} onChange={setMovementReason} />
                <Field label={t("inventory.notes")} value={movementNotes} onChange={setMovementNotes} />
              </div>
            </div>
            )}
          </div>
        </EntityModal>
      )}

      {productEditor?.mode === "create" && (
        <Products autoOpenCreate onClose={() => { void closeProductEditor(); }} />
      )}

      {productEditor?.mode === "edit" && (
        <Products autoOpenEditProductId={productEditor.productId} onClose={() => { void closeProductEditor(); }} />
      )}

      {productToDisable && (
        <ConfirmDialog
          title={t("products.disableProduct")}
          message={t("products.disableProductConfirm").replace("{name}", productToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setProductToDisable(null)}
          onConfirm={() => { void disableProduct(productToDisable); }}
        />
      )}

      {productToActivate && (
        <ConfirmDialog
          title={t("products.activateProduct")}
          message={t("products.activateProductConfirm").replace("{name}", productToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setProductToActivate(null)}
          onConfirm={() => { void activateProduct(productToActivate); }}
        />
      )}

      {productToDelete && (
        <ConfirmDialog
          title={t("products.deleteProduct")}
          message={t("products.deleteProductConfirm").replace("{name}", productToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setProductToDelete(null)}
          onConfirm={() => { void deleteProduct(productToDelete); }}
        />
      )}
    </section>
  );
}

function ProductActionMenu({
  product,
  onEdit,
  onActivate,
  onDisable,
  onDelete,
}: {
  product: InventoryProductSummary;
  onEdit: () => void;
  onActivate: () => void;
  onDisable: () => void;
  onDelete: () => void;
}) {
  const { t } = useI18n();

  return (
    <details className="action-menu">
      <summary aria-label={t("common.options")}>
        <MoreVertical size={18} />
      </summary>
      <div className="action-menu-content">
        <button type="button" onClick={onEdit}><Edit3 size={16} />{t("common.edit")}</button>
        {product.status === "Inactive" ? (
          <button type="button" onClick={onActivate}><CheckCircle2 size={16} />{t("common.activate")}</button>
        ) : (
          <button type="button" onClick={onDisable}><PowerOff size={16} />{t("common.disable")}</button>
        )}
        <button type="button" className="danger" onClick={onDelete}><Trash2 size={16} />{t("common.delete")}</button>
      </div>
    </details>
  );
}

function MovementVariantRow({
  type,
  productName,
  variant,
  locations,
  value,
  onChange,
}: {
  type: MovementType;
  productName: string;
  variant: InventoryVariant;
  locations: StockLocation[];
  value?: MovementItemDraft;
  onChange: (value: MovementItemDraft) => void;
}) {
  const { t } = useI18n();
  const current = value ?? { selected: false, stockLocationId: locations[0]?.id ?? "", quantity: "1", unitCost: "" };
  const overAvailable = type === "Exit" && (parseInt(current.quantity) || 0) > variant.availableQuantity;

  return (
    <div className="store-group">
      <label className="toggle">
        <input
          type="checkbox"
          checked={current.selected}
          onChange={(event) => onChange({ ...current, selected: event.target.checked })}
        />
        <span>{variant.sku} - {variantDisplayName(productName, variant)}</span>
      </label>
      {current.selected && (
        <div className="form-grid compact">
          <label className="field">
            <span>{t("inventory.stockLocation")}</span>
            <select value={current.stockLocationId} onChange={(event) => onChange({ ...current, stockLocationId: event.target.value })}>
              {locations.map((location) => <option key={location.id} value={location.id}>{location.name}</option>)}
            </select>
          </label>
          <Field label={type === "Adjustment" ? t("inventory.finalQuantity") : t("inventory.quantity")} value={current.quantity} required onChange={(quantity) => onChange({ ...current, quantity })} />
          {type === "Entry" && <Field label={t("inventory.unitCost")} value={current.unitCost} onChange={(unitCost) => onChange({ ...current, unitCost })} />}
          <div className={overAvailable ? "context-pill error" : "context-pill"}>
            {t("inventory.availableQuantity")}: {variant.availableQuantity}
          </div>
        </div>
      )}
    </div>
  );
}

function VariantLabel({ productName, variant }: { productName: string; variant: InventoryVariant }) {
  if (variant.attributes.length > 0) {
    return (
      <div className="variant-chip-row">
        {variant.attributes.map((attribute) => (
          <span key={`${variant.productVariantId}-${attribute.attributeName}-${attribute.code}`} className="inventory-badge">
            {attribute.valueName}
          </span>
        ))}
      </div>
    );
  }

  return <span>{variantDisplayName(productName, variant)}</span>;
}

function variantDisplayName(productName: string, variant: InventoryVariant) {
  if (variant.attributes.length > 0) {
    return variant.attributes.map((attribute) => attribute.valueName).join(" / ");
  }

  return variant.name
    .replace(productName, "")
    .replace(/^[-–—\s/]+/, "")
    .trim() || variant.name;
}

function movementTitle(type: MovementType, t: (key: string) => string) {
  if (type === "Entry") return t("inventory.registerEntryTitle");
  if (type === "Exit") return t("inventory.registerExitTitle");
  return t("inventory.registerAdjustmentTitle");
}
