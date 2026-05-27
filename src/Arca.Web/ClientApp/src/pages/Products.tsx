import { useState } from "react";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { Toggle } from "../components/Toggle";
import { api } from "../api";
import type {
  ProductSummary,
  ProductAttribute,
  AttributeValue,
  Category,
  ProductType,
  ProductVariant,
  ProductImage,
  Pagination,
} from "../types";
import { slugify } from "../utils/validation";

type SelectedVariantAttr = {
  productAttributeId: string;
  productAttributeValueIds: string[];
};

export function Products() {
  const [tenantId, setTenantId] = useState("");
  const [products, setProducts] = useState<ProductSummary[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);

  const [categories, setCategories] = useState<Category[]>([]);
  const [productTypes, setProductTypes] = useState<ProductType[]>([]);
  const [attributes, setAttributes] = useState<ProductAttribute[]>([]);

  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [baseSku, setBaseSku] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [productTypeId, setProductTypeId] = useState("");
  const [barcode, setBarcode] = useState("");
  const [brand, setBrand] = useState("");
  const [status, setStatus] = useState("Active");
  const [salePrice, setSalePrice] = useState("0");
  const [costPrice, setCostPrice] = useState("");

  const [selectedVariantAttrs, setSelectedVariantAttrs] = useState<SelectedVariantAttr[]>([]);
  const [previewResults, setPreviewResults] = useState<ProductVariant[]>([]);

  const [selectedProductId, setSelectedProductId] = useState<string | null>(null);
  const [selectedProductName, setSelectedProductName] = useState("");
  const [variants, setVariants] = useState<ProductVariant[]>([]);
  const [images, setImages] = useState<ProductImage[]>([]);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imageVariantId, setImageVariantId] = useState("");
  const [imageAltText, setImageAltText] = useState("");
  const [imageSortOrder, setImageSortOrder] = useState("0");
  const [imageIsMain, setImageIsMain] = useState(false);

  async function loadProducts(nextPage = page) {
    if (!tenantId.trim()) { setMessage("TenantId is required."); return; }
    setIsLoading(true);
    setMessage(null);
    try {
      const [prodData, catData, ptData, attrData] = await Promise.all([
        api.catalog.products.list(tenantId.trim(), {
          page: nextPage,
          pageSize: 25,
          search,
        }),
        api.catalog.categories.list(tenantId.trim()),
        api.catalog.productTypes.list(tenantId.trim()),
        api.catalog.attributes.list(tenantId.trim()),
      ]);
      setProducts(prodData.products);
      setPagination(prodData.pagination ?? null);
      setPage(prodData.pagination?.page ?? nextPage);
      setCategories(catData.categories);
      setProductTypes(ptData.productTypes);
      setAttributes(attrData.attributes);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to load data.");
    } finally {
      setIsLoading(false);
    }
  }

  function resetForm() {
    setName("");
    setSlug("");
    setBaseSku("");
    setDescription("");
    setCategoryId("");
    setProductTypeId("");
    setBarcode("");
    setBrand("");
    setStatus("Active");
    setSalePrice("0");
    setCostPrice("");
    setSelectedVariantAttrs([]);
    setPreviewResults([]);
    setMessage(null);
  }

  function toggleVariantAttr(attrId: string, valueId: string) {
    setSelectedVariantAttrs((prev) => {
      const existing = prev.find((a) => a.productAttributeId === attrId);
      if (existing) {
        const ids = existing.productAttributeValueIds.includes(valueId)
          ? existing.productAttributeValueIds.filter((id) => id !== valueId)
          : [...existing.productAttributeValueIds, valueId];
        const updated = prev.map((a) =>
          a.productAttributeId === attrId ? { ...a, productAttributeValueIds: ids } : a
        );
        return ids.length === 0
          ? updated.filter((a) => a.productAttributeId !== attrId)
          : updated;
      }
      return [...prev, { productAttributeId: attrId, productAttributeValueIds: [valueId] }];
    });
  }

  async function previewVariants() {
    if (!baseSku.trim() || !name.trim()) {
      setMessage("Name and Base SKU are required.");
      return;
    }
    try {
      const data = await api.catalog.products.variants.preview({
        tenantId: tenantId.trim(),
        productName: name.trim(),
        baseSku: baseSku.trim().toUpperCase(),
        defaultSalePrice: parseFloat(salePrice) || 0,
        defaultCostPrice: costPrice ? parseFloat(costPrice) : null,
        status,
        variantAttributes: selectedVariantAttrs,
      });
      setPreviewResults((data as { variants: ProductVariant[] }).variants);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to preview variants.");
    }
  }

  async function createProduct() {
    if (!name.trim() || !baseSku.trim() || !slug.trim()) {
      setMessage("Name, Slug and Base SKU are required.");
      return;
    }
    try {
      await api.catalog.products.create({
        tenantId: tenantId.trim(),
        categoryId: categoryId || null,
        productTypeId: productTypeId || null,
        productName: name.trim(),
        slug: slug.trim(),
        description: description.trim() || null,
        baseSku: baseSku.trim().toUpperCase(),
        barcode: barcode.trim() || null,
        brand: brand.trim() || null,
        status,
        defaultSalePrice: parseFloat(salePrice) || 0,
        defaultCostPrice: costPrice ? parseFloat(costPrice) : null,
        variantAttributes: selectedVariantAttrs,
      });
      setMessage("Product created.");
      resetForm();
      await loadProducts();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to create product.");
    }
  }

  async function loadProductWorkspace(product: ProductSummary) {
    setSelectedProductId(product.id);
    setSelectedProductName(product.name);
    setImageFile(null);
    setImageVariantId("");
    setImageAltText("");
    setImageSortOrder("0");
    setImageIsMain(false);

    try {
      const [variantData, imageData] = await Promise.all([
        api.catalog.products.variants.list(product.id, tenantId.trim()),
        api.catalog.products.images.list(product.id, tenantId.trim()),
      ]);
      setVariants(variantData.variants);
      setImages(imageData.images);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to load product details.");
    }
  }

  async function loadImages(productId: string) {
    const data = await api.catalog.products.images.list(productId, tenantId.trim());
    setImages(data.images);
  }

  async function uploadImage() {
    if (!selectedProductId) {
      setMessage("Select a product before uploading an image.");
      return;
    }

    if (!imageFile) {
      setMessage("Choose an image file before uploading.");
      return;
    }

    try {
      const formData = new FormData();
      formData.append("TenantId", tenantId.trim());
      formData.append("File", imageFile);
      formData.append("AltText", imageAltText.trim());
      formData.append("SortOrder", imageSortOrder.trim() || "0");
      formData.append("IsMain", imageIsMain ? "true" : "false");
      if (imageVariantId) {
        formData.append("ProductVariantId", imageVariantId);
      }

      await api.catalog.products.images.upload(selectedProductId, formData);
      setMessage("Image uploaded.");
      setImageFile(null);
      setImageVariantId("");
      setImageAltText("");
      setImageSortOrder("0");
      setImageIsMain(false);
      await loadImages(selectedProductId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to upload image.");
    }
  }

  async function deleteImage(imageId: string) {
    if (!selectedProductId) return;

    try {
      await api.catalog.products.images.delete(selectedProductId, imageId, tenantId.trim());
      setMessage("Image deleted.");
      await loadImages(selectedProductId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to delete image.");
    }
  }

  async function disableProduct(id: string) {
    try {
      await api.catalog.products.disable(id, tenantId.trim());
      setMessage("Product disabled.");
      await loadProducts();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to disable product.");
    }
  }

  const variantAttrs = attributes.filter((a) => a.isVariantAttribute && a.isActive);

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Products</h2>
            <p>Manage products and generate variants.</p>
          </div>
          <div className="row-actions">
            <button className="secondary" onClick={() => loadProducts(1)} disabled={isLoading}>
              {isLoading ? "Loading..." : "Load Data"}
            </button>
            <button className="primary" onClick={() => { resetForm(); setShowForm(!showForm); }}>
              {showForm ? "Cancel" : "New Product"}
            </button>
          </div>
        </div>
        <div className="form-grid">
          <Field label="TenantId" value={tenantId} required onChange={setTenantId} />
          <Field label="Search" value={search} onChange={setSearch} />
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      {showForm && (
        <div className="panel-section">
          <div className="section-heading">
            <div>
              <h2>New Product</h2>
              <p>Create a product with optional variant generation.</p>
            </div>
          </div>
          <div className="form-grid">
            <Field label="Product Name" value={name} required onChange={(v) => { setName(v); setSlug(slugify(v)); }} />
            <Field label="Slug" value={slug} required onChange={setSlug} />
            <Field label="Base SKU" value={baseSku} required onChange={setBaseSku} />
            <Field label="Description" value={description} onChange={setDescription} />
            <Field label="Barcode" value={barcode} onChange={setBarcode} />
            <Field label="Brand" value={brand} onChange={setBrand} />
            <Field label="Default Sale Price" value={salePrice} onChange={setSalePrice} />
            <Field label="Default Cost Price" value={costPrice} onChange={setCostPrice} />
            <label className="field">
              <span>Category</span>
              <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                <option value="">None</option>
                {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </label>
            <label className="field">
              <span>Product Type</span>
              <select value={productTypeId} onChange={(e) => setProductTypeId(e.target.value)}>
                <option value="">None</option>
                {productTypes.map((pt) => <option key={pt.id} value={pt.id}>{pt.name}</option>)}
              </select>
            </label>
            <label className="field">
              <span>Status</span>
              <select value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="Draft">Draft</option>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
            </label>
          </div>

          {variantAttrs.length > 0 && (
            <div className="permission-groups" style={{ marginTop: "1rem" }}>
              {variantAttrs.map((attr) => (
                <div key={attr.id} className="permission-group">
                  <strong>{attr.name} ({attr.code})</strong>
                  {attr.id && (
                    <AttributeValueSelector
                      attributeId={attr.id}
                      tenantId={tenantId}
                      selectedIds={selectedVariantAttrs.find((a) => a.productAttributeId === attr.id)?.productAttributeValueIds ?? []}
                      onToggle={(valueId) => toggleVariantAttr(attr.id, valueId)}
                    />
                  )}
                </div>
              ))}
            </div>
          )}

          <div className="actions left">
            <button className="secondary" onClick={previewVariants}>Preview Variants</button>
            <button className="primary" onClick={createProduct}>Create Product</button>
          </div>

          {previewResults.length > 0 && (
            <div className="table-shell" style={{ marginTop: "1rem" }}>
              <table>
                <thead>
                  <tr><th>SKU</th><th>Name</th><th>Price</th><th>Status</th></tr>
                </thead>
                <tbody>
                  {previewResults.map((v, i) => (
                    <tr key={i}>
                      <td>{v.sku}</td>
                      <td>{v.name}</td>
                      <td>{v.defaultSalePrice}</td>
                      <td>{v.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      <div className="table-shell">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>SKU</th>
              <th>Category</th>
              <th>Brand</th>
              <th>Variants</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {products.length === 0 ? (
              <tr><td colSpan={7}>No products loaded.</td></tr>
            ) : (
              products.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td>{p.baseSku}</td>
                  <td>{p.categoryId?.slice(0, 8) ?? "-"}</td>
                  <td>{p.brand || "-"}</td>
                  <td>{p.variantCount}</td>
                  <td>{p.status}</td>
                  <td>
                    <div className="row-actions">
                      <button className="secondary" onClick={() => loadProductWorkspace(p)}>
                        Manage
                      </button>
                      {p.status !== "Inactive" && (
                        <button className="secondary" onClick={() => disableProduct(p.id)}>
                          Disable
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadProducts(nextPage)} />
      </div>

      {selectedProductId && (
        <div className="panel-section">
          <div className="section-heading">
            <div>
              <h2>Product Details</h2>
              <p>{selectedProductName}</p>
            </div>
          </div>
          <div className="table-shell">
            <table>
              <thead>
                <tr><th>SKU</th><th>Name</th><th>Sale Price</th><th>Cost Price</th><th>Status</th></tr>
              </thead>
              <tbody>
                {variants.length === 0 ? (
                  <tr><td colSpan={5}>No variants.</td></tr>
                ) : (
                  variants.map((v) => (
                    <tr key={v.id}>
                      <td>{v.sku}</td>
                      <td>{v.name}</td>
                      <td>{v.defaultSalePrice.toFixed(2)}</td>
                      <td>{v.defaultCostPrice?.toFixed(2) ?? "-"}</td>
                      <td>{v.status}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="image-management">
            <div className="section-heading">
              <div>
                <h2>Images</h2>
                <p>Upload product images or associate them with a specific variant.</p>
              </div>
            </div>

            <div className="form-grid">
              <label className="field">
                <span>Image File</span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  onChange={(event) => setImageFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <label className="field">
                <span>Variant</span>
                <select value={imageVariantId} onChange={(event) => setImageVariantId(event.target.value)}>
                  <option value="">Product image</option>
                  {variants.map((variant) => (
                    <option key={variant.id} value={variant.id}>
                      {variant.sku}
                    </option>
                  ))}
                </select>
              </label>
              <Field label="Alt Text" value={imageAltText} onChange={setImageAltText} />
              <Field label="Sort Order" value={imageSortOrder} onChange={setImageSortOrder} />
              <Toggle label="Main Image" checked={imageIsMain} onChange={setImageIsMain} />
            </div>

            <div className="actions left">
              <button className="primary" onClick={uploadImage}>Upload Image</button>
            </div>

            <div className="image-grid">
              {images.length === 0 ? (
                <div className="empty-state">No images uploaded for this product.</div>
              ) : (
                images.map((image) => (
                  <article key={image.id} className="product-image">
                    <div className="product-image-preview">
                      <img src={image.publicUrl ?? image.storagePath} alt={image.altText ?? image.originalFileName} />
                    </div>
                    <div className="product-image-meta">
                      <strong>{image.originalFileName}</strong>
                      <span>{image.isMain ? "Main image" : "Gallery image"}</span>
                      <span>{image.productVariantId ? `Variant ${image.productVariantId.slice(0, 8)}` : "Product-level"}</span>
                      <span>Order {image.sortOrder}</span>
                    </div>
                    <button className="secondary" onClick={() => deleteImage(image.id)}>
                      Delete
                    </button>
                  </article>
                ))
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

function AttributeValueSelector({
  attributeId,
  tenantId,
  selectedIds,
  onToggle,
}: {
  attributeId: string;
  tenantId: string;
  selectedIds: string[];
  onToggle: (valueId: string) => void;
}) {
  const [values, setValues] = useState<AttributeValue[]>([]);
  const [loaded, setLoaded] = useState(false);

  if (!loaded) {
    api.catalog.attributes.values.list(attributeId, tenantId).then((data) => {
      setValues(data.values.filter((v) => v.isActive));
      setLoaded(true);
    });
  }

  if (!loaded) return <small>Loading values...</small>;

  return (
    <div className="permission-row">
      {values.map((v) => (
        <Toggle
          key={v.id}
          label={v.name}
          checked={selectedIds.includes(v.id)}
          onChange={() => onToggle(v.id)}
        />
      ))}
    </div>
  );
}
