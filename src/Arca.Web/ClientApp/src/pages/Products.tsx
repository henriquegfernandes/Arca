import { useEffect, useState } from "react";
import { CheckCircle2, Edit3, ImagePlus, PackagePlus, PowerOff, Trash2, Upload } from "lucide-react";
import { ConfirmDialog, DetailGrid, EntityModal, PageHeader, SearchInput } from "../components/Crud";
import { Field } from "../components/Field";
import { PaginationControls } from "../components/PaginationControls";
import { ProductImageThumb } from "../components/ProductImageThumb";
import { Toggle } from "../components/Toggle";
import { api } from "../api";
import { useAppContext } from "../context/AppContext";
import { useI18n } from "../i18n";
import { buildCategoryOptions, getCategoryPathLabel } from "../utils/categories";
import type {
  ProductSummary,
  ProductDetails,
  ProductAttribute,
  AttributeValue,
  Category,
  ProductType,
  ProductVariant,
  ProductImage,
  Pagination,
  StockLocation,
} from "../types";
import { slugify } from "../utils/validation";

type SelectedVariantAttr = {
  productAttributeId: string;
  productAttributeValueIds: string[];
};

type ProductVariantDraft = {
  id: string;
  sku: string;
  barcode: string;
  name: string;
  defaultSalePrice: string;
  defaultCostPrice: string;
  status: string;
  selected: boolean;
  initialStockQuantity: string;
};

type ProductsProps = {
  autoOpenCreate?: boolean;
  autoOpenEditProductId?: string | null;
  onClose?: () => void;
};

export function Products({ autoOpenCreate = false, autoOpenEditProductId = null, onClose }: ProductsProps = {}) {
  const { currentTenant, currentStore } = useAppContext();
  const { t } = useI18n();
  const tenantId = currentTenant?.id ?? "";
  const storeId = currentStore?.id ?? "";
  const [products, setProducts] = useState<ProductSummary[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<Pagination | null>(null);

  const [categories, setCategories] = useState<Category[]>([]);
  const [productTypes, setProductTypes] = useState<ProductType[]>([]);
  const [attributes, setAttributes] = useState<ProductAttribute[]>([]);
  const [productTypeAttributes, setProductTypeAttributes] = useState<ProductAttribute[]>([]);
  const [stockLocations, setStockLocations] = useState<StockLocation[]>([]);

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
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
  const [initialStockLocationId, setInitialStockLocationId] = useState("");
  const [initialStockQuantity, setInitialStockQuantity] = useState("0");

  const [selectedVariantAttrs, setSelectedVariantAttrs] = useState<SelectedVariantAttr[]>([]);
  const [enabledVariantAttributeIds, setEnabledVariantAttributeIds] = useState<string[]>([]);
  const [singlePriceMode, setSinglePriceMode] = useState(true);
  const [previewResults, setPreviewResults] = useState<ProductVariant[]>([]);
  const [variantDrafts, setVariantDrafts] = useState<ProductVariantDraft[]>([]);

  const [selectedProductId, setSelectedProductId] = useState<string | null>(null);
  const [selectedProductName, setSelectedProductName] = useState("");
  const [selectedProductDetails, setSelectedProductDetails] = useState<ProductDetails | null>(null);
  const [variants, setVariants] = useState<ProductVariant[]>([]);
  const [images, setImages] = useState<ProductImage[]>([]);
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imageVariantId, setImageVariantId] = useState("");
  const [imageAltText, setImageAltText] = useState("");
  const [imageSortOrder, setImageSortOrder] = useState("0");
  const [imageIsMain, setImageIsMain] = useState(false);
  const [isManageModalOpen, setIsManageModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editingProductId, setEditingProductId] = useState<string | null>(null);
  const [productToDisable, setProductToDisable] = useState<ProductSummary | null>(null);
  const [productToActivate, setProductToActivate] = useState<ProductSummary | null>(null);
  const [productToDelete, setProductToDelete] = useState<ProductSummary | null>(null);
  const [imageToDelete, setImageToDelete] = useState<ProductImage | null>(null);
  const [autoEditorHandled, setAutoEditorHandled] = useState(false);

  useEffect(() => {
    if (!tenantId) {
      setProducts([]);
      setPagination(null);
      setCategories([]);
      setProductTypes([]);
      setAttributes([]);
      setSelectedProductId(null);
      setSelectedProductDetails(null);
      setIsManageModalOpen(false);
      return;
    }

    void loadProducts(1);
  }, [tenantId, storeId]);

  useEffect(() => {
    if (!tenantId || autoEditorHandled || (!autoOpenCreate && !autoOpenEditProductId)) return;

    if (autoOpenCreate) {
      resetForm();
      setIsCreateModalOpen(true);
      setAutoEditorHandled(true);
      return;
    }

    if (autoOpenEditProductId) {
      setAutoEditorHandled(true);
      void openEditProductById(autoOpenEditProductId);
    }
  }, [tenantId, autoEditorHandled, autoOpenCreate, autoOpenEditProductId]);

  useEffect(() => {
    if (!tenantId || !productTypeId) {
      setProductTypeAttributes([]);
      return;
    }

    api.catalog.productTypes.attributes(productTypeId, tenantId)
      .then((data) => {
        setProductTypeAttributes(data.attributes);
        setEnabledVariantAttributeIds(data.attributes
          .filter((attribute) => attribute.isActive && attribute.isVariantAttribute && canAttributeGenerateVariations(attribute))
          .map((attribute) => attribute.id));
      })
      .catch(() => setProductTypeAttributes([]));
  }, [tenantId, productTypeId]);

  async function loadProducts(nextPage = page) {
    if (!tenantId) { setMessage(t("products.selectTenantLoad")); return; }
    setIsLoading(true);
    setMessage(null);
    try {
      const [prodData, catData, ptData, attrData, stockData] = await Promise.all([
        api.catalog.products.list(tenantId.trim(), {
          page: nextPage,
          pageSize: 25,
          search,
        }),
        api.catalog.categories.list(tenantId.trim()),
        api.catalog.productTypes.list(tenantId.trim()),
        api.catalog.attributes.list(tenantId.trim()),
        storeId ? api.inventory.stockLocations(tenantId.trim(), storeId) : Promise.resolve({ stockLocations: [] }),
      ]);
      setProducts(prodData.products);
      setPagination(prodData.pagination ?? null);
      setPage(prodData.pagination?.page ?? nextPage);
      setCategories(catData.categories);
      setProductTypes(ptData.productTypes);
      setAttributes(attrData.attributes);
      setStockLocations(stockData.stockLocations);
      if (stockData.stockLocations.length > 0) {
        setInitialStockLocationId((current) => current || stockData.stockLocations[0].id);
      }
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.loadFailed"));
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
    setInitialStockLocationId(stockLocations[0]?.id ?? "");
    setInitialStockQuantity("0");
    setSelectedVariantAttrs([]);
    setEnabledVariantAttributeIds([]);
    setSinglePriceMode(true);
    setPreviewResults([]);
    setVariantDrafts([]);
    setWizardStep(0);
    setMessage(null);
  }

  function openCreateWizard() {
    resetForm();
    setIsCreateModalOpen(true);
  }

  function closeCreateWizard() {
    setIsCreateModalOpen(false);
    if (autoOpenCreate) onClose?.();
  }

  function closeEditWizard() {
    setIsEditModalOpen(false);
    setEditingProductId(null);
    if (autoOpenEditProductId) onClose?.();
  }

  function populateProductForm(product: ProductDetails | ProductSummary) {
    setName(product.name);
    setSlug(product.slug);
    setBaseSku(product.baseSku);
    setDescription(product.description ?? "");
    setCategoryId(product.categoryId ?? "");
    setProductTypeId(product.productTypeId ?? "");
    setBarcode(product.barcode ?? "");
    setBrand(product.brand ?? "");
    setStatus(product.status);
    setSelectedVariantAttrs([]);
    setPreviewResults([]);
    setVariantDrafts([]);
    setSinglePriceMode(true);
    setWizardStep(0);
  }

  function populateVariantDrafts(nextVariants: ProductVariant[]) {
    setVariantDrafts(nextVariants.map((variant) => ({
      id: variant.id,
      sku: variant.sku,
      barcode: variant.barcode ?? "",
      name: variant.name,
      defaultSalePrice: variant.defaultSalePrice.toString(),
      defaultCostPrice: variant.defaultCostPrice?.toString() ?? "",
      status: variant.status,
      selected: true,
      initialStockQuantity: "0",
    })));
    const variantSelections = new Map<string, Set<string>>();
    nextVariants.forEach((variant) => {
      variant.attributes?.forEach((attribute) => {
        if (!variantSelections.has(attribute.productAttributeId)) {
          variantSelections.set(attribute.productAttributeId, new Set<string>());
        }
        variantSelections.get(attribute.productAttributeId)?.add(attribute.productAttributeValueId);
      });
    });
    const selectedAttrs = Array.from(variantSelections.entries()).map(([productAttributeId, values]) => ({
      productAttributeId,
      productAttributeValueIds: Array.from(values),
    }));
    setSelectedVariantAttrs(selectedAttrs);
    setEnabledVariantAttributeIds((current) => Array.from(new Set([
      ...current,
      ...selectedAttrs.map((attribute) => attribute.productAttributeId),
    ])));
  }

  function startEditProduct(product: ProductDetails, nextVariants = variants, nextImages = images) {
    populateProductForm(product);
    populateVariantDrafts(nextVariants);
    setVariants(nextVariants);
    setImages(nextImages);
    setSelectedProductId(product.id);
    setSelectedProductName(product.name);
    setSelectedProductDetails(product);
    setEditingProductId(product.id);
    setIsManageModalOpen(false);
    setIsEditModalOpen(true);
  }

  async function openEditProduct(product: ProductSummary) {
    if (!tenantId) return;

    try {
      const [details, variantData, imageData] = await Promise.all([
        api.catalog.products.get(product.id, tenantId.trim()),
        api.catalog.products.variants.list(product.id, tenantId.trim()),
        api.catalog.products.images.list(product.id, tenantId.trim()),
      ]);
      setSelectedProductDetails(details);
      startEditProduct(details, variantData.variants, imageData.images);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.detailsLoadFailed"));
    }
  }

  async function openEditProductById(productId: string) {
    if (!tenantId) return;

    try {
      const [details, variantData, imageData] = await Promise.all([
        api.catalog.products.get(productId, tenantId.trim()),
        api.catalog.products.variants.list(productId, tenantId.trim()),
        api.catalog.products.images.list(productId, tenantId.trim()),
      ]);
      setSelectedProductDetails(details);
      startEditProduct(details, variantData.variants, imageData.images);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.detailsLoadFailed"));
      onClose?.();
    }
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

  function toggleVariantGeneratorAttribute(attrId: string) {
    setEnabledVariantAttributeIds((current) =>
      current.includes(attrId)
        ? current.filter((id) => id !== attrId)
        : [...current, attrId]
    );
    setSelectedVariantAttrs((current) => current.filter((attribute) => attribute.productAttributeId !== attrId));
  }

  function applySinglePriceToDrafts(nextSalePrice = salePrice, nextCostPrice = costPrice) {
    if (!singlePriceMode) return;
    setVariantDrafts((current) => current.map((variant) => ({
      ...variant,
      defaultSalePrice: nextSalePrice,
      defaultCostPrice: nextCostPrice,
    })));
  }

  function applyInitialStockToDrafts(nextQuantity = initialStockQuantity) {
    setVariantDrafts((current) => current.map((variant) => ({
      ...variant,
      initialStockQuantity: variant.initialStockQuantity || nextQuantity,
    })));
  }

  function nextWizardStep() {
    if (wizardStep === 2) {
      void previewVariants();
    }
    setWizardStep((step) => Math.min(wizardSteps.length - 1, step + 1));
  }

  async function previewVariants() {
    if (!baseSku.trim() || !name.trim()) {
      setMessage(t("products.nameAndSkuRequired"));
      return;
    }
    if (!tenantId) {
      setMessage(t("products.selectTenantPreview"));
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
      const generatedVariants = (data as { variants: ProductVariant[] }).variants;
      setPreviewResults(generatedVariants);
      const generatedDrafts = generatedVariants.map((variant) => {
        const existingDraft = variantDrafts.find((draft) => draft.sku.trim().toUpperCase() === variant.sku.trim().toUpperCase());
        return existingDraft ?? {
          id: `new:${variant.sku}`,
          sku: variant.sku,
          barcode: variant.barcode ?? "",
          name: variant.name,
          defaultSalePrice: salePrice,
          defaultCostPrice: costPrice,
          status: variant.status,
          selected: true,
          initialStockQuantity,
        };
      });

      if (editingProductId) {
        const existingIds = new Set(variantDrafts.map((draft) => draft.sku.trim().toUpperCase()));
        setVariantDrafts([
          ...variantDrafts,
          ...generatedDrafts.filter((draft) => !existingIds.has(draft.sku.trim().toUpperCase())),
        ]);
      } else {
        setVariantDrafts(generatedDrafts);
      }
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.previewFailed"));
    }
  }

  async function createProduct() {
    if (!name.trim() || !baseSku.trim() || !slug.trim()) {
      setMessage(t("products.nameSlugSkuRequired"));
      return;
    }
    if (!tenantId) {
      setMessage(t("products.selectTenantCreate"));
      return;
    }
    try {
      const created = await api.catalog.products.create({
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
      const createdProductId = (created as { productId: string }).productId;
      const refreshedVariants = await api.catalog.products.variants.list(createdProductId, tenantId.trim());
      const draftBySku = new Map(variantDrafts.map((draft) => [draft.sku.trim().toUpperCase(), draft]));
      const selectedVariants = refreshedVariants.variants.filter((variant) => {
        const draft = draftBySku.get(variant.sku.trim().toUpperCase());
        return draft?.selected ?? true;
      });
      const variantsToDelete = refreshedVariants.variants.filter((variant) => {
        const draft = draftBySku.get(variant.sku.trim().toUpperCase());
        return draft?.selected === false;
      });

      if (variantsToDelete.length > 0) {
        await Promise.all(variantsToDelete.map((variant) =>
          api.catalog.products.variants.delete(createdProductId, variant.id, tenantId.trim())
        ));
      }

      const pricedVariants = selectedVariants.map((variant) => {
        const draft = draftBySku.get(variant.sku.trim().toUpperCase());
        return {
          id: variant.id,
          sku: (draft?.sku ?? variant.sku).trim().toUpperCase(),
          barcode: draft?.barcode.trim() || variant.barcode,
          name: draft?.name.trim() || variant.name,
          defaultSalePrice: parseFloat(draft?.defaultSalePrice ?? salePrice) || 0,
          defaultCostPrice: draft?.defaultCostPrice ? parseFloat(draft.defaultCostPrice) : null,
          status: draft?.status ?? variant.status,
        };
      });

      if (pricedVariants.length > 0) {
        await api.catalog.products.variants.update(createdProductId, {
          tenantId: tenantId.trim(),
          variants: pricedVariants,
        });
      }

      if (storeId && initialStockLocationId && selectedVariants.length > 0) {
        await Promise.all(selectedVariants.map((variant) => {
          const draft = draftBySku.get(variant.sku.trim().toUpperCase());
          const initialQuantity = parseInt(draft?.initialStockQuantity ?? initialStockQuantity) || 0;
          if (initialQuantity <= 0) return Promise.resolve();

          return api.inventory.entry({
            tenantId: tenantId.trim(),
            storeId,
            stockLocationId: initialStockLocationId,
            productVariantId: variant.id,
            quantity: initialQuantity,
            unitCost: draft?.defaultCostPrice ? parseFloat(draft.defaultCostPrice) : costPrice ? parseFloat(costPrice) : null,
            reason: t("products.initialStockReason"),
            notes: t("products.initialStockNotes"),
            batchNumber: null,
          });
        }));
      }
      setMessage(t("products.productCreated"));
      resetForm();
      setIsCreateModalOpen(false);
      await loadProducts();
      if (autoOpenCreate) onClose?.();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.createFailed"));
    }
  }

  async function updateProduct() {
    if (!editingProductId) return;
    if (!name.trim() || !baseSku.trim() || !slug.trim()) {
      setMessage(t("products.nameSlugSkuRequired"));
      return;
    }
    if (!tenantId) {
      setMessage(t("products.selectTenantUpdate"));
      return;
    }

    try {
      let addedVariantCount = 0;
      const variantsToDelete = variantDrafts.filter((variant) => variant.id && !variant.id.startsWith("new:") && !variant.selected);
      if (variantsToDelete.length > 0) {
        const shouldDelete = window.confirm(
          t("products.deleteUnselectedVariantsConfirm").replace("{{count}}", String(variantsToDelete.length))
        );
        if (!shouldDelete) return;
      }

      await api.catalog.products.update(editingProductId, {
        tenantId: tenantId.trim(),
        categoryId: categoryId || null,
        productTypeId: productTypeId || null,
        name: name.trim(),
        slug: slug.trim(),
        description: description.trim() || null,
        baseSku: baseSku.trim().toUpperCase(),
        barcode: barcode.trim() || null,
        brand: brand.trim() || null,
        status,
      });
      if (variantDrafts.length > 0) {
        const variantPayload = variantDrafts
          .filter((variant) => variant.selected && !variant.id.startsWith("new:"))
          .map((variant) => ({
          id: variant.id,
          sku: variant.sku.trim().toUpperCase(),
          barcode: variant.barcode.trim() || null,
          name: variant.name.trim(),
          defaultSalePrice: parseFloat(variant.defaultSalePrice) || 0,
          defaultCostPrice: variant.defaultCostPrice ? parseFloat(variant.defaultCostPrice) : null,
          status: variant.status,
        }));
        if (variantPayload.length > 0) {
          const updatedVariants = await api.catalog.products.variants.update(editingProductId, {
            tenantId: tenantId.trim(),
            variants: variantPayload,
          });
          setVariants(updatedVariants.variants);
          populateVariantDrafts(updatedVariants.variants);
        }
      }

      if (selectedVariantAttrs.some((attribute) => attribute.productAttributeValueIds.length > 0)) {
        const added = await api.catalog.products.variants.addGenerated(editingProductId, {
          tenantId: tenantId.trim(),
          productName: name.trim(),
          baseSku: baseSku.trim().toUpperCase(),
          defaultSalePrice: parseFloat(salePrice) || 0,
          defaultCostPrice: costPrice ? parseFloat(costPrice) : null,
          status,
          variantAttributes: selectedVariantAttrs,
        });
        addedVariantCount = (added.variants ?? []).length;
      }

      let refreshedVariants = await api.catalog.products.variants.list(editingProductId, tenantId.trim());
      const draftBySku = new Map(variantDrafts.map((draft) => [draft.sku.trim().toUpperCase(), draft]));
      const uncheckedNewVariants = refreshedVariants.variants.filter((variant) => {
        const draft = draftBySku.get(variant.sku.trim().toUpperCase());
        return draft?.selected === false;
      });
      const allDeleteTargets = [
        ...variantsToDelete.map((variant) => ({ id: variant.id })),
        ...uncheckedNewVariants.filter((variant) => !variantsToDelete.some((target) => target.id === variant.id)),
      ];
      if (allDeleteTargets.length > 0) {
        await Promise.all(allDeleteTargets.map((variant) =>
          api.catalog.products.variants.delete(editingProductId, variant.id, tenantId.trim())
        ));
        refreshedVariants = await api.catalog.products.variants.list(editingProductId, tenantId.trim());
      }

      const pricedVariants = refreshedVariants.variants
        .map((variant) => {
          const draft = draftBySku.get(variant.sku.trim().toUpperCase());
          if (!draft || !draft.selected) return null;
          return {
            id: variant.id,
            sku: draft.sku.trim().toUpperCase(),
            barcode: draft.barcode.trim() || null,
            name: draft.name.trim(),
            defaultSalePrice: parseFloat(draft.defaultSalePrice) || 0,
            defaultCostPrice: draft.defaultCostPrice ? parseFloat(draft.defaultCostPrice) : null,
            status: draft.status,
          };
        })
        .filter((variant): variant is {
          id: string;
          sku: string;
          barcode: string | null;
          name: string;
          defaultSalePrice: number;
          defaultCostPrice: number | null;
          status: string;
        } => variant !== null);

      if (pricedVariants.length > 0) {
        const updatedVariants = await api.catalog.products.variants.update(editingProductId, {
          tenantId: tenantId.trim(),
          variants: pricedVariants,
        });
        setVariants(updatedVariants.variants);
        populateVariantDrafts(updatedVariants.variants);
      } else {
        setVariants(refreshedVariants.variants);
        populateVariantDrafts(refreshedVariants.variants);
      }

      setMessage(
        addedVariantCount > 0
          ? t("products.productUpdatedWithVariants").replace("{{count}}", String(addedVariantCount))
          : t("products.productUpdated")
      );
      setIsEditModalOpen(false);
      setEditingProductId(null);
      await loadProducts();
      if (autoOpenEditProductId) onClose?.();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.updateFailed"));
    }
  }

  async function loadProductWorkspace(product: ProductSummary) {
    setSelectedProductId(product.id);
    setSelectedProductName(product.name);
    setSelectedProductDetails(null);
    setImageFile(null);
    setImageVariantId("");
    setImageAltText("");
    setImageSortOrder("0");
    setImageIsMain(false);

    try {
      const [details, variantData, imageData] = await Promise.all([
        api.catalog.products.get(product.id, tenantId.trim()),
        api.catalog.products.variants.list(product.id, tenantId.trim()),
        api.catalog.products.images.list(product.id, tenantId.trim()),
      ]);
      setSelectedProductDetails(details);
      setVariants(variantData.variants);
      setImages(imageData.images);
      setIsManageModalOpen(true);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.detailsLoadFailed"));
    }
  }

  async function loadImages(productId: string) {
    const data = await api.catalog.products.images.list(productId, tenantId.trim());
    setImages(data.images);
  }

  async function uploadImage() {
    if (!selectedProductId) {
      setMessage(t("products.selectProductBeforeUpload"));
      return;
    }

    if (!imageFile) {
      setMessage(t("products.chooseImageBeforeUpload"));
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
      setMessage(t("products.imageUploaded"));
      setImageFile(null);
      setImageVariantId("");
      setImageAltText("");
      setImageSortOrder("0");
      setImageIsMain(false);
      await loadImages(selectedProductId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.imageUploadFailed"));
    }
  }

  async function deleteImage(imageId: string) {
    if (!selectedProductId) return;

    try {
      await api.catalog.products.images.delete(selectedProductId, imageId, tenantId.trim());
      setMessage(t("products.imageDeleted"));
      await loadImages(selectedProductId);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.imageDeleteFailed"));
    }
  }

  async function disableProduct(id: string) {
    try {
      await api.catalog.products.disable(id, tenantId.trim());
      setMessage(t("products.productDisabled"));
      setProductToDisable(null);
      await loadProducts();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productDisableFailed"));
    }
  }

  async function activateProduct(id: string) {
    try {
      await api.catalog.products.activate(id, tenantId.trim());
      setMessage(t("products.productActivated"));
      setProductToActivate(null);
      await loadProducts();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productActivateFailed"));
    }
  }

  async function deleteProduct(id: string) {
    try {
      await api.catalog.products.delete(id, tenantId.trim());
      setMessage(t("products.productDeleted"));
      setProductToDelete(null);
      await loadProducts();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : t("products.productDeleteFailed"));
    }
  }

  function updateVariantDraft(id: string, patch: Partial<ProductVariantDraft>) {
    setVariantDrafts((current) =>
      current.map((variant) => variant.id === id ? { ...variant, ...patch } : variant)
    );
  }

  const activeCategories = categories.filter((category) => category.isActive);
  const activeProductTypes = productTypes.filter((productType) => productType.isActive);
  const attributesForType = (productTypeId && productTypeAttributes.length > 0
    ? productTypeAttributes
    : attributes).filter((attribute) => attribute.isActive);
  const variantAttrs = attributesForType.filter((a) =>
    a.isActive && enabledVariantAttributeIds.includes(a.id) && canAttributeGenerateVariations(a)
  );
  const existingVariantSkus = new Set(variantDrafts
    .filter((variant) => !variant.id.startsWith("new:"))
    .map((variant) => variant.sku.trim().toUpperCase()));
  const newPreviewResults = previewResults.filter((variant) => !existingVariantSkus.has(variant.sku.trim().toUpperCase()));
  const isEmbeddedEditor = autoOpenCreate || Boolean(autoOpenEditProductId);
  const wizardSteps = [
    t("products.basicData"),
    t("products.typeAttributes"),
    t("products.variations"),
    t("products.pricingAndStock"),
    t("products.review"),
  ];

  return (
    <section className={isEmbeddedEditor ? "embedded-editor-host" : "stores-panel"}>
      {!isEmbeddedEditor && (
        <>
          <div className="panel-section">
            <PageHeader
              title={t("products.title")}
              description={t("products.description")}
              actions={<button className="primary" type="button" onClick={openCreateWizard}><PackagePlus size={16} />{t("products.addNew")}</button>}
            />
            <SearchInput value={search} onChange={setSearch} onSearch={() => loadProducts(1)} isLoading={isLoading} />
            {message && (
              <div className={isNoticeSuccess(message) ? "notice success" : "notice error"}>
                {message}
              </div>
            )}
          </div>

      {isCreateModalOpen && (
        <EntityModal
          title={t("products.newProduct")}
          size="wide"
          onClose={closeCreateWizard}
          footer={(
            <>
              <button className="secondary" type="button" onClick={closeCreateWizard}>{t("common.cancel")}</button>
              {wizardStep > 0 && (
                <button className="secondary" type="button" onClick={() => setWizardStep((step) => Math.max(0, step - 1))}>{t("common.back")}</button>
              )}
              {(wizardStep === 2 || wizardStep === 3) && (
                <button className="secondary" type="button" onClick={previewVariants}>{t("common.refresh")} {t("products.preview")}</button>
              )}
              {wizardStep < wizardSteps.length - 1 ? (
                <button className="primary" type="button" onClick={nextWizardStep}>{t("common.next")}</button>
              ) : (
                <button className="primary" type="button" onClick={createProduct}>{t("products.createProduct")}</button>
              )}
            </>
          )}
        >
          <div className="wizard-steps">
            {wizardSteps.map((step, index) => (
              <button
                key={step}
                type="button"
                className={index === wizardStep ? "wizard-step active" : "wizard-step"}
                onClick={() => setWizardStep(index)}
              >
                {index + 1}. {step}
              </button>
            ))}
          </div>

          {wizardStep === 0 && (
            <div className="form-grid">
              <Field label={t("products.name")} value={name} required onChange={(v) => { setName(v); setSlug(slugify(v)); }} />
              <label className="field">
                <span>{t("products.productType")}</span>
                <select value={productTypeId} onChange={(e) => setProductTypeId(e.target.value)}>
                  <option value="">{t("common.none")}</option>
                  {activeProductTypes.map((pt) => <option key={pt.id} value={pt.id}>{pt.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>{t("products.category")}</span>
                <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                  <option value="">{t("common.none")}</option>
                  {buildCategoryOptions(activeCategories).map((category) => (
                    <option key={category.id} value={category.id}>{category.label}</option>
                  ))}
                </select>
              </label>
              <Field label={t("products.baseSku")} value={baseSku} required onChange={setBaseSku} />
              <Field label={t("products.brand")} value={brand} onChange={setBrand} />
              <Field label={t("products.slug")} value={slug} required onChange={setSlug} />
              <Field label={t("common.description")} value={description} onChange={setDescription} />
              <Field label={t("products.barcode")} value={barcode} onChange={setBarcode} />
              <label className="field">
                <span>{t("common.status")}</span>
                <select value={status} onChange={(e) => setStatus(e.target.value)}>
                  <option value="Draft">{t("common.draft")}</option>
                  <option value="Active">{t("common.active")}</option>
                  <option value="Inactive">{t("common.inactive")}</option>
                </select>
              </label>
            </div>
          )}

          {wizardStep === 1 && (
            <div className="permission-groups">
              {attributesForType.length === 0 ? (
                <div className="empty-state">{t("products.noConfiguredAttributes")}</div>
              ) : (
                attributesForType.map((attr) => (
                  <div key={attr.id} className="permission-group">
                    <div className="variant-attribute-row">
                      <div>
                        <strong>{attr.name}</strong>
                        <span>{attr.code} · {attr.attributeType}</span>
                        <span>{attr.isRequired ? t("products.required") : t("products.optional")}</span>
                      </div>
                      {canAttributeGenerateVariations(attr) ? (
                        <Toggle
                          label={t("products.generateVariationsWithAttribute")}
                          checked={enabledVariantAttributeIds.includes(attr.id)}
                          onChange={() => toggleVariantGeneratorAttribute(attr.id)}
                        />
                      ) : (
                        <span>{t("products.productDetailAttribute")}</span>
                      )}
                    </div>
                  </div>
                ))
              )}
            </div>
          )}

          {wizardStep === 2 && (
            <>
              <p className="context-hint">{t("products.chooseVariantValues")}</p>
              {variantAttrs.length === 0 ? (
                <div className="empty-state">{t("products.noVariantAttributes")}</div>
              ) : (
                <div className="permission-groups">
                  {variantAttrs.map((attr) => (
                    <div key={attr.id} className="permission-group">
                      <strong>{attr.name} ({attr.code})</strong>
                      <AttributeValueSelector
                        attributeId={attr.id}
                        tenantId={tenantId}
                        selectedIds={selectedVariantAttrs.find((a) => a.productAttributeId === attr.id)?.productAttributeValueIds ?? []}
                        onToggle={(valueId) => toggleVariantAttr(attr.id, valueId)}
                      />
                    </div>
                  ))}
                </div>
              )}
            </>
          )}

          {wizardStep === 3 && (
            <div className="stack">
              <p className="context-hint">{t("products.priceAndStockHint")}</p>
              <div className="form-grid compact">
                <Toggle label={t("products.singlePrice")} checked={singlePriceMode} onChange={setSinglePriceMode} />
                {singlePriceMode && (
                  <>
                    <Field label={t("products.defaultSalePrice")} value={salePrice} onChange={(value) => { setSalePrice(value); applySinglePriceToDrafts(value, costPrice); }} />
                    <Field label={t("products.defaultCostPrice")} value={costPrice} onChange={(value) => { setCostPrice(value); applySinglePriceToDrafts(salePrice, value); }} />
                  </>
                )}
                <label className="field">
                  <span>{t("products.initialStockLocation")}</span>
                  <select value={initialStockLocationId} onChange={(event) => setInitialStockLocationId(event.target.value)}>
                    <option value="">{t("products.doNotCreateInitialStock")}</option>
                    {stockLocations.map((location) => (
                      <option key={location.id} value={location.id}>{location.name}</option>
                    ))}
                  </select>
                </label>
                <Field label={t("products.initialStockPerVariant")} value={initialStockQuantity} onChange={(value) => { setInitialStockQuantity(value); applyInitialStockToDrafts(value); }} />
                <div className={currentStore ? "context-pill" : "context-pill error"}>
                  {currentStore ? `${t("header.store")}: ${currentStore.name}` : t("products.selectStoreForInitialStock")}
                </div>
              </div>

              <VariantPricingTable
                drafts={variantDrafts}
                singlePriceMode={singlePriceMode}
                t={t}
                onChange={updateVariantDraft}
              />
            </div>
          )}

          {wizardStep === 4 && (
            <div className="review">
              <div><span>{t("products.name")}</span><strong>{name || "-"}</strong></div>
              <div><span>{t("products.productType")}</span><strong>{activeProductTypes.find((pt) => pt.id === productTypeId)?.name ?? "-"}</strong></div>
              <div><span>{t("products.category")}</span><strong>{getCategoryPathLabel(activeCategories, categoryId)}</strong></div>
              <div><span>{t("products.baseSku")}</span><strong>{baseSku || "-"}</strong></div>
              <div><span>{t("products.brand")}</span><strong>{brand || "-"}</strong></div>
              <div><span>{t("products.variants")}</span><strong>{variantDrafts.filter((variant) => variant.selected).length || t("products.previewNotGenerated")}</strong></div>
              <div><span>{t("products.salePrice")}</span><strong>{salePrice || "0"}</strong></div>
              <div><span>{t("products.initialStock")}</span><strong>{initialStockLocationId ? t("products.initialStockPerVariantValue").replace("{{quantity}}", initialStockQuantity || "0") : t("products.notApplied")}</strong></div>
            </div>
          )}
        </EntityModal>
      )}

          <div className="table-shell">
            <table>
              <thead>
                <tr>
                  <th>{t("products.name")}</th>
                  <th>{t("products.sku")}</th>
                  <th>{t("products.category")}</th>
                  <th>{t("products.brand")}</th>
                  <th>{t("products.variants")}</th>
                  <th>{t("common.status")}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {products.length === 0 ? (
                  <tr><td colSpan={7}>{t("products.noProductsFound")}</td></tr>
                ) : (
                  products.map((p) => (
                    <tr key={p.id}>
                      <td>
                        <div className="product-cell">
                          <ProductImageThumb src={p.mainImageUrl} alt={p.name} size="sm" />
                          <button className="row-link" onClick={() => loadProductWorkspace(p)}>{p.name}</button>
                        </div>
                      </td>
                      <td>{p.baseSku}</td>
                      <td>{getCategoryPathLabel(categories, p.categoryId)}</td>
                      <td>{p.brand || "-"}</td>
                      <td>{p.variantCount}</td>
                      <td>{p.status}</td>
                      <td>
                        <div className="row-actions">
                          <button className="secondary" onClick={() => openEditProduct(p)}>
                            <Edit3 size={16} />
                            {t("common.edit")}
                          </button>
                          <button className="secondary" onClick={() => loadProductWorkspace(p)}>
                            <ImagePlus size={16} />
                            {t("common.manage")}
                          </button>
                          {p.status !== "Inactive" ? (
                            <button className="secondary" onClick={() => setProductToDisable(p)}>
                              <PowerOff size={16} />
                              {t("common.disable")}
                            </button>
                          ) : (
                            <button className="secondary" onClick={() => setProductToActivate(p)}>
                              <CheckCircle2 size={16} />
                              {t("common.activate")}
                            </button>
                          )}
                          <button className="secondary danger" onClick={() => setProductToDelete(p)}>
                            <Trash2 size={16} />
                            {t("common.delete")}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
            <PaginationControls pagination={pagination} onPageChange={(nextPage) => loadProducts(nextPage)} />
          </div>
        </>
      )}

      {selectedProductId && isManageModalOpen && (
        <EntityModal
          title={t("products.productDetails")}
          onClose={() => setIsManageModalOpen(false)}
          footer={(
            <>
              <button className="secondary" type="button" onClick={() => setIsManageModalOpen(false)}>{t("common.close")}</button>
              {selectedProductDetails && (
                <button className="primary" type="button" onClick={() => startEditProduct(selectedProductDetails)}>{t("common.edit")}</button>
              )}
            </>
          )}
        >
          <div className="section-heading">
            <div>
              <p>{selectedProductName}</p>
            </div>
          </div>
          {selectedProductDetails && (
            <DetailGrid
              items={[
                { label: t("products.name"), value: selectedProductDetails.name },
                { label: t("products.productType"), value: selectedProductDetails.productTypeName ?? "-" },
                { label: t("products.category"), value: selectedProductDetails.categoryName ?? "-" },
                { label: t("products.baseSku"), value: selectedProductDetails.baseSku },
                { label: t("products.brand"), value: selectedProductDetails.brand ?? "-" },
                { label: t("common.status"), value: selectedProductDetails.status },
              ]}
            />
          )}
          <div className="table-shell">
            <table>
              <thead>
                <tr><th>{t("products.sku")}</th><th>{t("products.name")}</th><th>{t("products.salePrice")}</th><th>{t("products.costPrice")}</th><th>{t("common.status")}</th></tr>
              </thead>
              <tbody>
                {variants.length === 0 ? (
                  <tr><td colSpan={5}>{t("products.noVariants")}</td></tr>
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
                <h2>{t("products.images")}</h2>
                <p>{t("products.imagesDescription")}</p>
              </div>
            </div>

            <div className="form-grid">
              <label className="field">
                <span>{t("products.imageFile")}</span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  onChange={(event) => setImageFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <label className="field">
                <span>{t("inventory.variant")}</span>
                <select value={imageVariantId} onChange={(event) => setImageVariantId(event.target.value)}>
                  <option value="">{t("products.productImage")}</option>
                  {variants.map((variant) => (
                    <option key={variant.id} value={variant.id}>
                      {variant.sku}
                    </option>
                  ))}
                </select>
              </label>
              <Field label={t("products.altText")} value={imageAltText} onChange={setImageAltText} />
              <Field label={t("products.sortOrder")} value={imageSortOrder} onChange={setImageSortOrder} />
              <Toggle label={t("products.mainImage")} checked={imageIsMain} onChange={setImageIsMain} />
            </div>

            <div className="actions left">
              <button className="primary" onClick={uploadImage}><Upload size={16} />{t("products.uploadImage")}</button>
            </div>

            <div className="image-grid">
              {images.length === 0 ? (
                <div className="empty-state">{t("products.noImages")}</div>
              ) : (
                images.map((image) => (
                  <article key={image.id} className="product-image">
                    <div className="product-image-preview">
                      <img src={image.publicUrl ?? image.storagePath} alt={image.altText ?? image.originalFileName} />
                    </div>
                    <div className="product-image-meta">
                      <strong>{image.originalFileName}</strong>
                      <span>{image.isMain ? t("products.mainImage") : t("products.galleryImage")}</span>
                      <span>{image.productVariantId ? `${t("inventory.variant")} ${image.productVariantId.slice(0, 8)}` : t("products.productLevel")}</span>
                      <span>{t("products.orderValue").replace("{{order}}", String(image.sortOrder))}</span>
                    </div>
                    <button className="secondary" onClick={() => setImageToDelete(image)}>
                      <Trash2 size={16} />
                      {t("common.delete")}
                    </button>
                  </article>
                ))
              )}
            </div>
          </div>
        </EntityModal>
      )}

      {isEditModalOpen && (
        <EntityModal
          title={t("products.editProduct")}
          size="wide"
          onClose={closeEditWizard}
          footer={(
            <>
              <button className="secondary" type="button" onClick={closeEditWizard}>{t("common.cancel")}</button>
              {wizardStep > 0 && (
                <button className="secondary" type="button" onClick={() => setWizardStep((step) => Math.max(0, step - 1))}>{t("common.back")}</button>
              )}
              {(wizardStep === 2 || wizardStep === 3) && (
                <button className="secondary" type="button" onClick={previewVariants}>{t("common.refresh")} {t("products.preview")}</button>
              )}
              {wizardStep < wizardSteps.length - 1 ? (
                <button className="primary" type="button" onClick={nextWizardStep}>{t("common.next")}</button>
              ) : (
                <button className="primary" type="button" onClick={updateProduct}>{t("products.saveChanges")}</button>
              )}
            </>
          )}
        >
          <div className="wizard-steps">
            {wizardSteps.map((step, index) => (
              <button
                key={step}
                type="button"
                className={index === wizardStep ? "wizard-step active" : "wizard-step"}
                onClick={() => setWizardStep(index)}
              >
                {index + 1}. {step}
              </button>
            ))}
          </div>

          <div className="notice">
            {t("products.editingModeNotice")}
          </div>

          {wizardStep === 0 && (
            <div className="form-grid">
              <Field label={t("products.name")} value={name} required onChange={(v) => { setName(v); setSlug(slugify(v)); }} />
              <label className="field">
                <span>{t("products.productType")}</span>
                <select value={productTypeId} onChange={(e) => setProductTypeId(e.target.value)}>
                  <option value="">{t("common.none")}</option>
                  {activeProductTypes.map((pt) => <option key={pt.id} value={pt.id}>{pt.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>{t("products.category")}</span>
                <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                  <option value="">{t("common.none")}</option>
                  {buildCategoryOptions(activeCategories).map((category) => (
                    <option key={category.id} value={category.id}>{category.label}</option>
                  ))}
                </select>
              </label>
              <Field label={t("products.baseSku")} value={baseSku} required onChange={setBaseSku} />
              <Field label={t("products.brand")} value={brand} onChange={setBrand} />
              <Field label={t("products.slug")} value={slug} required onChange={setSlug} />
              <Field label={t("common.description")} value={description} onChange={setDescription} />
              <Field label={t("products.barcode")} value={barcode} onChange={setBarcode} />
              <label className="field">
                <span>{t("common.status")}</span>
                <select value={status} onChange={(e) => setStatus(e.target.value)}>
                  <option value="Draft">{t("common.draft")}</option>
                  <option value="Active">{t("common.active")}</option>
                  <option value="Inactive">{t("common.inactive")}</option>
                </select>
              </label>
            </div>
          )}

          {wizardStep === 1 && (
            <div className="permission-groups">
              {attributesForType.length === 0 ? (
                <div className="empty-state">{t("products.noConfiguredAttributes")}</div>
              ) : (
                attributesForType.map((attr) => (
                  <div key={attr.id} className="permission-group">
                    <div className="variant-attribute-row">
                      <div>
                        <strong>{attr.name}</strong>
                        <span>{attr.code} · {attr.attributeType}</span>
                        <span>{attr.isRequired ? t("products.required") : t("products.optional")}</span>
                      </div>
                      {canAttributeGenerateVariations(attr) ? (
                        <Toggle
                          label={t("products.generateVariationsWithAttribute")}
                          checked={enabledVariantAttributeIds.includes(attr.id)}
                          onChange={() => toggleVariantGeneratorAttribute(attr.id)}
                        />
                      ) : (
                        <span>{t("products.productDetailAttribute")}</span>
                      )}
                    </div>
                  </div>
                ))
              )}
            </div>
          )}

          {wizardStep === 2 && (
            <>
              <p className="context-hint">{t("products.editVariantSelectionHint")}</p>
              {variantAttrs.length === 0 ? (
                <div className="empty-state">{t("products.noVariantAttributes")}</div>
              ) : (
                <div className="permission-groups">
                  {variantAttrs.map((attr) => (
                    <div key={attr.id} className="permission-group">
                      <strong>{attr.name} ({attr.code})</strong>
                      <AttributeValueSelector
                        attributeId={attr.id}
                        tenantId={tenantId}
                        selectedIds={selectedVariantAttrs.find((a) => a.productAttributeId === attr.id)?.productAttributeValueIds ?? []}
                        onToggle={(valueId) => toggleVariantAttr(attr.id, valueId)}
                      />
                    </div>
                  ))}
                </div>
              )}
            </>
          )}

          {wizardStep === 3 && (
            <div className="stack">
              <p className="context-hint">{t("products.editPreviewHint")}</p>
              {previewResults.length > 0 && (
                <div className="table-shell">
                  <table>
                    <thead>
                      <tr><th>{t("products.previewSku")}</th><th>{t("products.name")}</th><th>{t("products.price")}</th><th>{t("common.status")}</th><th>{t("products.previewState")}</th></tr>
                    </thead>
                    <tbody>
                      {previewResults.map((variant, index) => (
                        <tr key={`${variant.sku}-${index}`}>
                          <td>{variant.sku}</td>
                          <td>{variant.name}</td>
                          <td>{variant.defaultSalePrice}</td>
                          <td>{variant.status}</td>
                          <td>{existingVariantSkus.has(variant.sku.trim().toUpperCase()) ? t("products.alreadyExists") : t("products.newCombination")}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {previewResults.length > 0 && (
                <div className={newPreviewResults.length > 0 ? "notice success" : "notice"}>
                  {newPreviewResults.length > 0
                    ? t("products.newCombinationsWillBeAdded").replace("{{count}}", String(newPreviewResults.length))
                    : t("products.noNewCombinations")}
                </div>
              )}
              <div className="form-grid compact">
                <Toggle label={t("products.singlePrice")} checked={singlePriceMode} onChange={setSinglePriceMode} />
                {singlePriceMode && (
                  <>
                    <Field label={t("products.defaultSalePrice")} value={salePrice} onChange={(value) => { setSalePrice(value); applySinglePriceToDrafts(value, costPrice); }} />
                    <Field label={t("products.defaultCostPrice")} value={costPrice} onChange={(value) => { setCostPrice(value); applySinglePriceToDrafts(salePrice, value); }} />
                  </>
                )}
                <div className="notice">
                  {t("products.initialStockEditNotice")}
                </div>
              </div>

              <VariantPricingTable
                drafts={variantDrafts}
                singlePriceMode={singlePriceMode}
                t={t}
                onChange={updateVariantDraft}
                isEdit
              />
            </div>
          )}

          {wizardStep === 4 && (
            <div className="review">
              <div><span>{t("products.mode")}</span><strong>{t("products.editExistingProduct")}</strong></div>
              <div><span>{t("products.name")}</span><strong>{name || "-"}</strong></div>
              <div><span>{t("products.productType")}</span><strong>{activeProductTypes.find((pt) => pt.id === productTypeId)?.name ?? "-"}</strong></div>
              <div><span>{t("products.category")}</span><strong>{getCategoryPathLabel(activeCategories, categoryId)}</strong></div>
              <div><span>{t("products.baseSku")}</span><strong>{baseSku || "-"}</strong></div>
              <div><span>{t("products.brand")}</span><strong>{brand || "-"}</strong></div>
              <div><span>{t("products.variants")}</span><strong>{variantDrafts.filter((variant) => variant.selected).length}</strong></div>
              <div><span>{t("products.newCombinations")}</span><strong>{newPreviewResults.length}</strong></div>
              <div><span>{t("products.images")}</span><strong>{images.length}</strong></div>
              <div><span>{t("products.stockChanges")}</span><strong>{t("products.handledInInventory")}</strong></div>
            </div>
          )}
        </EntityModal>
      )}

      {productToDisable && (
        <ConfirmDialog
          title={t("products.disableProduct")}
          message={t("products.disableProductConfirm").replace("{name}", productToDisable.name)}
          confirmLabel={t("common.disable")}
          onCancel={() => setProductToDisable(null)}
          onConfirm={() => disableProduct(productToDisable.id)}
        />
      )}

      {productToActivate && (
        <ConfirmDialog
          title={t("products.activateProduct")}
          message={t("products.activateProductConfirm").replace("{name}", productToActivate.name)}
          confirmLabel={t("common.activate")}
          onCancel={() => setProductToActivate(null)}
          onConfirm={() => activateProduct(productToActivate.id)}
        />
      )}

      {productToDelete && (
        <ConfirmDialog
          title={t("products.deleteProduct")}
          message={t("products.deleteProductConfirm").replace("{name}", productToDelete.name)}
          confirmLabel={t("common.delete")}
          onCancel={() => setProductToDelete(null)}
          onConfirm={() => deleteProduct(productToDelete.id)}
        />
      )}

      {imageToDelete && (
        <ConfirmDialog
          title={t("products.deleteImage")}
          message={t("products.deleteImageConfirm").replace("{{name}}", imageToDelete.originalFileName)}
          confirmLabel={t("common.delete")}
          onCancel={() => setImageToDelete(null)}
          onConfirm={() => {
            const id = imageToDelete.id;
            setImageToDelete(null);
            void deleteImage(id);
          }}
        />
      )}
    </section>
  );
}

function VariantPricingTable({
  drafts,
  singlePriceMode,
  t,
  onChange,
  isEdit = false,
}: {
  drafts: ProductVariantDraft[];
  singlePriceMode: boolean;
  t: (key: string) => string;
  onChange: (id: string, patch: Partial<ProductVariantDraft>) => void;
  isEdit?: boolean;
}) {
  return (
    <div className="table-shell">
      <table>
        <thead>
          <tr>
            <th>{t("common.active")}</th>
            <th>{t("products.sku")}</th>
            <th>{t("products.name")}</th>
            <th>{t("products.barcode")}</th>
            {!singlePriceMode && (
              <>
                <th>{t("products.salePrice")}</th>
                <th>{t("products.costPrice")}</th>
              </>
            )}
            {!isEdit && <th>{t("products.initialStock")}</th>}
            <th>{t("common.status")}</th>
          </tr>
        </thead>
        <tbody>
          {drafts.length === 0 ? (
            <tr><td colSpan={singlePriceMode ? 6 : 8}>{t("products.noVariantsLoaded")}</td></tr>
          ) : (
            drafts.map((variant) => (
              <tr key={variant.id} className={!variant.selected ? "muted-row" : undefined}>
                <td>
                  <input
                    type="checkbox"
                    checked={variant.selected}
                    onChange={(event) => onChange(variant.id, { selected: event.target.checked })}
                    aria-label={t("products.includeVariant")}
                  />
                </td>
                <td>
                  <input value={variant.sku} onChange={(event) => onChange(variant.id, { sku: event.target.value })} />
                </td>
                <td>
                  <input value={variant.name} onChange={(event) => onChange(variant.id, { name: event.target.value })} />
                </td>
                <td>
                  <input value={variant.barcode} onChange={(event) => onChange(variant.id, { barcode: event.target.value })} />
                </td>
                {!singlePriceMode && (
                  <>
                    <td>
                      <input value={variant.defaultSalePrice} onChange={(event) => onChange(variant.id, { defaultSalePrice: event.target.value })} />
                    </td>
                    <td>
                      <input value={variant.defaultCostPrice} onChange={(event) => onChange(variant.id, { defaultCostPrice: event.target.value })} />
                    </td>
                  </>
                )}
                {!isEdit && (
                  <td>
                    <input value={variant.initialStockQuantity} onChange={(event) => onChange(variant.id, { initialStockQuantity: event.target.value })} />
                  </td>
                )}
                <td>
                  <select value={variant.status} onChange={(event) => onChange(variant.id, { status: event.target.value })}>
                    <option value="Draft">{t("common.draft")}</option>
                    <option value="Active">{t("common.active")}</option>
                    <option value="Inactive">{t("common.inactive")}</option>
                  </select>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

function canAttributeGenerateVariations(attribute: ProductAttribute) {
  return ["Select", "MultiSelect"].includes(attribute.attributeType);
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
  const { t } = useI18n();
  const [values, setValues] = useState<AttributeValue[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    let isActive = true;
    setLoaded(false);

    api.catalog.attributes.values.list(attributeId, tenantId).then((data) => {
      if (!isActive) return;
      setValues(data.values.filter((v) => v.isActive));
      setLoaded(true);
    });

    return () => {
      isActive = false;
    };
  }, [attributeId, tenantId]);

  if (!loaded) return <small>{t("products.loadingValues")}</small>;

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

function isNoticeSuccess(message: string) {
  return !/(failed|required|select|choose|falha|obrigat|selecione|escolha|erro|inválid|invalid)/i.test(message);
}
