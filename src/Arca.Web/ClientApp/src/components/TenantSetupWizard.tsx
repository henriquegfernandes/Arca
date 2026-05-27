import { useState, useMemo } from "react";
import { Check, ChevronLeft, ChevronRight, Plus, Trash2 } from "lucide-react";
import { Field } from "./Field";
import { Toggle } from "./Toggle";
import { slugify, normalizeStoreCode, addRequired, addEmail, addPattern } from "../utils/validation";
import { api } from "../api";
import type { TenantSetupDraft, StoreDraft, ValidationErrors } from "../types";

const steps = ["Company", "Settings", "Stores", "Admin", "Catalog", "Review"];

const initialStore: StoreDraft = {
  name: "Matriz",
  code: "MATRIZ",
  document: "",
  email: "",
  phone: "",
  addressLine: "",
  city: "",
  state: "",
  zipCode: "",
  type: "Physical",
};

const initialDraft: TenantSetupDraft = {
  company: { name: "", legalName: "", document: "", slug: "", email: "", phone: "", mainSegment: "General" },
  settings: {
    currency: "BRL",
    timeZone: "America/Sao_Paulo",
    defaultLanguage: "pt-BR",
    allowMultipleStores: true,
    allowBatchControl: false,
    allowExpirationControl: false,
    allowStoreSpecificPricing: true,
  },
  stores: [initialStore],
  administrator: { fullName: "", email: "", phone: "", temporaryPassword: "ChangeMe!12345", sendInviteEmail: false },
  catalog: { template: "Fashion" },
};

export function TenantSetupWizard() {
  const [step, setStep] = useState(0);
  const [draft, setDraft] = useState<TenantSetupDraft>(initialDraft);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});

  const summary = useMemo(
    () => [
      { label: "Company", value: draft.company.name || "Not set" },
      { label: "Slug", value: draft.company.slug || "Not set" },
      { label: "Stores", value: String(draft.stores.length) },
      { label: "Catalog", value: draft.catalog.template },
    ],
    [draft]
  );

  async function submitTenantSetup() {
    const validation = validateAllSteps(draft);
    if (!validation.isValid) {
      setErrors(validation.errors);
      setStep(validation.firstInvalidStep);
      setMessage("Review the highlighted fields before creating the tenant.");
      return;
    }

    setIsSubmitting(true);
    setMessage(null);

    try {
      const payload = await api.tenants.setup(draft);
      setMessage(`Tenant created: ${payload.tenantId}`);
      setDraft(initialDraft);
      setStep(0);
      setErrors({});
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Tenant setup failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function goToStep(nextStep: number) {
    setMessage(null);
    if (nextStep <= step) {
      setStep(nextStep);
      return;
    }
    const validation = validateStepsBefore(nextStep, draft);
    setErrors(validation.errors);
    if (!validation.isValid) {
      setStep(validation.firstInvalidStep);
      setMessage("Complete the required fields before moving forward.");
      return;
    }
    setStep(nextStep);
  }

  function goNext() {
    const validation = validateStep(step, draft);
    setErrors(validation.errors);
    setMessage(null);
    if (!validation.isValid) {
      setMessage("Complete the required fields before moving forward.");
      return;
    }
    setStep(Math.min(steps.length - 1, step + 1));
  }

  return (
    <div className="tenant-workflow">
      <div className="step-rail" aria-label="Tenant setup progress">
        {steps.map((label, index) => (
          <button
            key={label}
            className={index === step ? "step active" : index < step ? "step done" : "step"}
            onClick={() => goToStep(index)}
          >
            <span>{index < step ? <Check size={14} /> : index + 1}</span>
            {label}
          </button>
        ))}
      </div>

      <div className="workflow-body">
        {step === 0 && (
          <div className="form-grid">
            <Field label="Name" value={draft.company.name} error={errors["company.name"]} required onChange={(value) => setDraft({ ...draft, company: { ...draft.company, name: value, slug: slugify(value) } })} />
            <Field label="Slug" value={draft.company.slug} error={errors["company.slug"]} required onChange={(value) => setDraft({ ...draft, company: { ...draft.company, slug: slugify(value) } })} />
            <Field label="Legal Name" value={draft.company.legalName} onChange={(value) => setDraft({ ...draft, company: { ...draft.company, legalName: value } })} />
            <Field label="Document" value={draft.company.document} onChange={(value) => setDraft({ ...draft, company: { ...draft.company, document: value } })} />
            <Field label="Email" type="email" value={draft.company.email} error={errors["company.email"]} onChange={(value) => setDraft({ ...draft, company: { ...draft.company, email: value } })} />
            <Field label="Phone" value={draft.company.phone} onChange={(value) => setDraft({ ...draft, company: { ...draft.company, phone: value } })} />
            <Field label="Main Segment" value={draft.company.mainSegment} error={errors["company.mainSegment"]} required onChange={(value) => setDraft({ ...draft, company: { ...draft.company, mainSegment: value } })} />
          </div>
        )}

        {step === 1 && (
          <div className="form-grid">
            <Field label="Currency" value={draft.settings.currency} error={errors["settings.currency"]} required onChange={(value) => setDraft({ ...draft, settings: { ...draft.settings, currency: value.toUpperCase() } })} />
            <Field label="Time Zone" value={draft.settings.timeZone} error={errors["settings.timeZone"]} required onChange={(value) => setDraft({ ...draft, settings: { ...draft.settings, timeZone: value } })} />
            <Field label="Language" value={draft.settings.defaultLanguage} error={errors["settings.defaultLanguage"]} required onChange={(value) => setDraft({ ...draft, settings: { ...draft.settings, defaultLanguage: value } })} />
            <Toggle label="Multiple Stores" checked={draft.settings.allowMultipleStores} onChange={(checked) => setDraft({ ...draft, settings: { ...draft.settings, allowMultipleStores: checked } })} />
            <Toggle label="Batch Control" checked={draft.settings.allowBatchControl} onChange={(checked) => setDraft({ ...draft, settings: { ...draft.settings, allowBatchControl: checked } })} />
            <Toggle label="Expiration Control" checked={draft.settings.allowExpirationControl} onChange={(checked) => setDraft({ ...draft, settings: { ...draft.settings, allowExpirationControl: checked } })} />
            <Toggle label="Store Pricing" checked={draft.settings.allowStoreSpecificPricing} onChange={(checked) => setDraft({ ...draft, settings: { ...draft.settings, allowStoreSpecificPricing: checked } })} />
          </div>
        )}

        {step === 2 && (
          <div className="store-list">
            {errors["stores"] && <div className="field-error">{errors["stores"]}</div>}
            {draft.stores.map((store, index) => (
              <div className="store-group" key={index}>
                <div className="store-group-header">
                  <strong>Store {index + 1}</strong>
                  {draft.stores.length > 1 && (
                    <button className="icon-only" title="Remove store" onClick={() => setDraft({ ...draft, stores: draft.stores.filter((_, i) => i !== index) })}>
                      <Trash2 size={16} />
                    </button>
                  )}
                </div>
                <div className="form-grid compact">
                  <Field label="Store Name" value={store.name} error={errors[`stores.${index}.name`]} required onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, name: value } : s) })} />
                  <Field label="Code" value={store.code} error={errors[`stores.${index}.code`]} required onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, code: normalizeStoreCode(value) } : s) })} />
                  <Field label="Type" value={store.type} error={errors[`stores.${index}.type`]} required onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, type: value } : s) })} />
                  <Field label="Email" type="email" value={store.email} error={errors[`stores.${index}.email`]} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, email: value } : s) })} />
                  <Field label="Phone" value={store.phone} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, phone: value } : s) })} />
                  <Field label="Document" value={store.document} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, document: value } : s) })} />
                  <Field label="Address" value={store.addressLine} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, addressLine: value } : s) })} />
                  <Field label="City" value={store.city} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, city: value } : s) })} />
                  <Field label="State" value={store.state} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, state: value } : s) })} />
                  <Field label="Zip Code" value={store.zipCode} onChange={(value) => setDraft({ ...draft, stores: draft.stores.map((s, i) => i === index ? { ...s, zipCode: value } : s) })} />
                </div>
              </div>
            ))}
            <button className="secondary add-store" onClick={() => setDraft({ ...draft, stores: [...draft.stores, { ...initialStore, code: `STORE${draft.stores.length + 1}` }] })}>
              <Plus size={16} />
              Add Store
            </button>
          </div>
        )}

        {step === 3 && (
          <div className="form-grid">
            <Field label="Full Name" value={draft.administrator.fullName} error={errors["administrator.fullName"]} required onChange={(value) => setDraft({ ...draft, administrator: { ...draft.administrator, fullName: value } })} />
            <Field label="Email" type="email" value={draft.administrator.email} error={errors["administrator.email"]} required onChange={(value) => setDraft({ ...draft, administrator: { ...draft.administrator, email: value } })} />
            <Field label="Phone" value={draft.administrator.phone} onChange={(value) => setDraft({ ...draft, administrator: { ...draft.administrator, phone: value } })} />
            <Field label="Temporary Password" type="password" value={draft.administrator.temporaryPassword} error={errors["administrator.temporaryPassword"]} required onChange={(value) => setDraft({ ...draft, administrator: { ...draft.administrator, temporaryPassword: value } })} />
          </div>
        )}

        {step === 4 && (
          <div className="template-grid">
            {["Fashion", "Shoes", "Electronics", "ReligiousGoods", "FoodBakery", "SnackBarRestaurant", "Market", "Custom"].map((template) => (
              <button
                key={template}
                className={draft.catalog.template === template ? "template selected" : "template"}
                onClick={() => setDraft({ ...draft, catalog: { template } })}
              >
                {template}
              </button>
            ))}
          </div>
        )}

        {step === 5 && (
          <div className="review">
            {summary.map((item) => (
              <div key={item.label}>
                <span>{item.label}</span>
                <strong>{item.value}</strong>
              </div>
            ))}
            <div>
              <span>Admin</span>
              <strong>{draft.administrator.email || "Not set"}</strong>
            </div>
          </div>
        )}

        {message && <div className={message.startsWith("Tenant created") ? "notice success" : "notice error"}>{message}</div>}

        <div className="actions">
          <button className="secondary" onClick={() => setStep(Math.max(0, step - 1))} disabled={step === 0}>
            <ChevronLeft size={16} />
            Back
          </button>
          {step < steps.length - 1 ? (
            <button className="primary" onClick={goNext}>
              Next
              <ChevronRight size={16} />
            </button>
          ) : (
            <button className="primary" onClick={submitTenantSetup} disabled={isSubmitting}>
              {isSubmitting ? "Creating..." : "Create Tenant"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

function validateAllSteps(draft: TenantSetupDraft) {
  return validateStepsBefore(steps.length, draft);
}

function validateStepsBefore(targetStep: number, draft: TenantSetupDraft) {
  const errors: ValidationErrors = {};
  let firstInvalidStep = 0;
  for (let index = 0; index < targetStep; index += 1) {
    const validation = validateStep(index, draft);
    Object.assign(errors, validation.errors);
    if (!validation.isValid && Object.keys(errors).length === Object.keys(validation.errors).length) {
      firstInvalidStep = index;
    }
  }
  return { isValid: Object.keys(errors).length === 0, errors, firstInvalidStep };
}

function validateStep(step: number, draft: TenantSetupDraft) {
  const errors: ValidationErrors = {};

  if (step === 0) {
    addRequired(errors, "company.name", draft.company.name, "Company name is required.");
    addRequired(errors, "company.slug", draft.company.slug, "Slug is required.");
    addPattern(errors, "company.slug", draft.company.slug, /^[a-z0-9]+(?:-[a-z0-9]+)*$/, "Use lowercase letters, numbers and hyphens.");
    addRequired(errors, "company.mainSegment", draft.company.mainSegment, "Main segment is required.");
    addEmail(errors, "company.email", draft.company.email);
  }

  if (step === 1) {
    addRequired(errors, "settings.currency", draft.settings.currency, "Currency is required.");
    addPattern(errors, "settings.currency", draft.settings.currency, /^[A-Z]{3}$/, "Use a 3-letter currency code.");
    addRequired(errors, "settings.timeZone", draft.settings.timeZone, "Time zone is required.");
    addPattern(errors, "settings.timeZone", draft.settings.timeZone, /^[A-Za-z_]+\/[A-Za-z_/-]+$/, "Use a valid IANA time zone.");
    addRequired(errors, "settings.defaultLanguage", draft.settings.defaultLanguage, "Language is required.");
    addPattern(errors, "settings.defaultLanguage", draft.settings.defaultLanguage, /^[a-z]{2}(?:-[A-Z]{2})?$/, "Use a locale such as pt-BR.");
  }

  if (step === 2) {
    if (draft.stores.length === 0) errors["stores"] = "Add at least one store.";
    const seenCodes = new Map<string, number>();
    draft.stores.forEach((store, index) => {
      addRequired(errors, `stores.${index}.name`, store.name, "Store name is required.");
      addRequired(errors, `stores.${index}.code`, store.code, "Store code is required.");
      addPattern(errors, `stores.${index}.code`, store.code, /^[A-Z0-9]+(?:-[A-Z0-9]+)*$/, "Use uppercase letters, numbers and hyphens.");
      addRequired(errors, `stores.${index}.type`, store.type, "Store type is required.");
      addEmail(errors, `stores.${index}.email`, store.email);
      const code = store.code.trim().toUpperCase();
      if (code) {
        const duplicateIndex = seenCodes.get(code);
        if (duplicateIndex !== undefined) {
          errors[`stores.${index}.code`] = "Store code must be unique.";
          errors[`stores.${duplicateIndex}.code`] = "Store code must be unique.";
        }
        seenCodes.set(code, index);
      }
    });
  }

  if (step === 3) {
    addRequired(errors, "administrator.fullName", draft.administrator.fullName, "Administrator name is required.");
    addRequired(errors, "administrator.email", draft.administrator.email, "Administrator email is required.");
    addEmail(errors, "administrator.email", draft.administrator.email);
    addRequired(errors, "administrator.temporaryPassword", draft.administrator.temporaryPassword, "Temporary password is required.");
    if (draft.administrator.temporaryPassword.trim() && draft.administrator.temporaryPassword.length < 10) {
      errors["administrator.temporaryPassword"] = "Use at least 10 characters.";
    }
  }

  if (step === 4 && !["Fashion", "Shoes", "Electronics", "ReligiousGoods", "FoodBakery", "SnackBarRestaurant", "Market", "Custom"].includes(draft.catalog.template)) {
    errors["catalog.template"] = "Choose a valid catalog template.";
  }

  return { isValid: Object.keys(errors).length === 0, errors };
}
