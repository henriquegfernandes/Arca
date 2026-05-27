import { useState } from "react";
import { Field } from "../components/Field";
import { api } from "../api";
import type { StockLocation, InventoryBalance, StockMovement } from "../types";

export function Inventory() {
  const [tenantId, setTenantId] = useState("");
  const [storeId, setStoreId] = useState("");
  const [stockLocations, setStockLocations] = useState<StockLocation[]>([]);
  const [message, setMessage] = useState<string | null>(null);

  const [stockLocationId, setStockLocationId] = useState("");
  const [variantId, setVariantId] = useState("");
  const [balance, setBalance] = useState<InventoryBalance | null>(null);

  const [movements, setMovements] = useState<StockMovement[]>([]);

  const [entryQuantity, setEntryQuantity] = useState("1");
  const [entryCost, setEntryCost] = useState("");
  const [entryReason, setEntryReason] = useState("Purchase");
  const [entryNotes, setEntryNotes] = useState("");
  const [entryBatch, setEntryBatch] = useState("");

  const [exitQuantity, setExitQuantity] = useState("1");
  const [exitType, setExitType] = useState("Sale");
  const [exitReason, setExitReason] = useState("");
  const [exitNotes, setExitNotes] = useState("");

  const [adjQuantity, setAdjQuantity] = useState("0");
  const [adjMinStock, setAdjMinStock] = useState("");
  const [adjReason, setAdjReason] = useState("");
  const [adjNotes, setAdjNotes] = useState("");

  async function loadStockLocations() {
    if (!tenantId.trim() || !storeId.trim()) {
      setMessage("TenantId and StoreId are required.");
      return;
    }
    setMessage(null);
    try {
      const data = await api.inventory.stockLocations(tenantId.trim(), storeId.trim());
      setStockLocations(data.stockLocations);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to load locations.");
    }
  }

  async function loadBalance() {
    if (!stockLocationId || !variantId) {
      setMessage("Stock Location and Variant are required.");
      return;
    }
    setMessage(null);
    try {
      const data = await api.inventory.balance(tenantId.trim(), storeId.trim(), stockLocationId, variantId);
      setBalance(data);
    } catch {
      setBalance(null);
      setMessage("No balance found or error loading.");
    }
  }

  async function loadMovements() {
    try {
      const data = await api.inventory.movements(tenantId.trim(), storeId.trim(), variantId || undefined, 50);
      setMovements(data.movements);
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to load movements.");
    }
  }

  async function registerEntry() {
    try {
      await api.inventory.entry({
        tenantId: tenantId.trim(),
        storeId: storeId.trim(),
        stockLocationId,
        productVariantId: variantId,
        quantity: parseInt(entryQuantity) || 1,
        unitCost: entryCost ? parseFloat(entryCost) : null,
        reason: entryReason,
        notes: entryNotes.trim() || null,
        batchNumber: entryBatch.trim() || null,
      });
      setMessage("Stock entry registered.");
      await loadBalance();
      await loadMovements();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to register entry.");
    }
  }

  async function registerExit() {
    try {
      await api.inventory.exit({
        tenantId: tenantId.trim(),
        storeId: storeId.trim(),
        stockLocationId,
        productVariantId: variantId,
        quantity: parseInt(exitQuantity) || 1,
        movementType: exitType,
        reason: exitReason.trim() || null,
        notes: exitNotes.trim() || null,
      });
      setMessage("Stock exit registered.");
      await loadBalance();
      await loadMovements();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to register exit.");
    }
  }

  async function adjustStock() {
    try {
      await api.inventory.adjust({
        tenantId: tenantId.trim(),
        storeId: storeId.trim(),
        stockLocationId,
        productVariantId: variantId,
        newQuantity: parseInt(adjQuantity) || 0,
        minimumStock: adjMinStock ? parseInt(adjMinStock) : null,
        reason: adjReason.trim() || null,
        notes: adjNotes.trim() || null,
      });
      setMessage("Stock adjusted.");
      await loadBalance();
      await loadMovements();
    } catch (err: unknown) {
      setMessage(err instanceof Error ? err.message : "Failed to adjust stock.");
    }
  }

  return (
    <section className="stores-panel">
      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h2>Inventory</h2>
            <p>Manage stock balances, entries, exits and adjustments.</p>
          </div>
        </div>
        <div className="form-grid">
          <Field label="TenantId" value={tenantId} required onChange={setTenantId} />
          <Field label="StoreId" value={storeId} required onChange={setStoreId} />
        </div>
        <div className="actions left">
          <button className="secondary" onClick={loadStockLocations}>Load Locations</button>
        </div>
        {message && <div className="notice error">{message}</div>}
      </div>

      {stockLocations.length > 0 && (
        <div className="panel-section">
          <h3>Stock Locations</h3>
          <div className="tenant-summary-grid">
            {stockLocations.map((loc) => (
              <button
                key={loc.id}
                className={stockLocationId === loc.id ? "tenant-summary selected" : "tenant-summary"}
                onClick={() => setStockLocationId(loc.id)}
              >
                <strong>{loc.name}</strong>
                <span>{loc.type}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      <div className="panel-section">
        <div className="form-grid">
          <Field label="Variant ID" value={variantId} required onChange={setVariantId} />
        </div>
        <div className="actions left">
          <button className="secondary" onClick={loadBalance}>Check Balance</button>
          <button className="secondary" onClick={loadMovements}>Load Movements</button>
        </div>

        {balance && (
          <div className="tenant-summary-grid" style={{ marginTop: "1rem" }}>
            <div className="metric">
              <span>Quantity</span>
              <strong>{balance.quantity}</strong>
            </div>
            <div className="metric">
              <span>Reserved</span>
              <strong>{balance.reservedQuantity}</strong>
            </div>
            <div className="metric accent">
              <span>Available</span>
              <strong>{balance.availableQuantity}</strong>
            </div>
            <div className="metric">
              <span>Min Stock</span>
              <strong>{balance.minimumStock}</strong>
            </div>
          </div>
        )}
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h3>Stock Entry</h3>
          </div>
        </div>
        <div className="form-grid">
          <Field label="Quantity" value={entryQuantity} required onChange={setEntryQuantity} />
          <Field label="Unit Cost" value={entryCost} onChange={setEntryCost} />
          <Field label="Batch" value={entryBatch} onChange={setEntryBatch} />
          <Field label="Reason" value={entryReason} onChange={setEntryReason} />
        </div>
        <Field label="Notes" value={entryNotes} onChange={setEntryNotes} />
        <div className="actions left">
          <button className="primary" onClick={registerEntry}>Register Entry</button>
        </div>
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h3>Stock Exit</h3>
          </div>
        </div>
        <div className="form-grid">
          <Field label="Quantity" value={exitQuantity} required onChange={setExitQuantity} />
          <label className="field">
            <span>Type</span>
            <select value={exitType} onChange={(e) => setExitType(e.target.value)}>
              <option value="Sale">Sale</option>
              <option value="TransferOut">Transfer Out</option>
              <option value="Loss">Loss</option>
              <option value="Consumption">Consumption</option>
            </select>
          </label>
          <Field label="Reason" value={exitReason} onChange={setExitReason} />
        </div>
        <Field label="Notes" value={exitNotes} onChange={setExitNotes} />
        <div className="actions left">
          <button className="primary" onClick={registerExit}>Register Exit</button>
        </div>
      </div>

      <div className="panel-section">
        <div className="section-heading">
          <div>
            <h3>Stock Adjustment</h3>
          </div>
        </div>
        <div className="form-grid">
          <Field label="New Quantity" value={adjQuantity} required onChange={setAdjQuantity} />
          <Field label="Min Stock" value={adjMinStock} onChange={setAdjMinStock} />
          <Field label="Reason" value={adjReason} onChange={setAdjReason} />
        </div>
        <Field label="Notes" value={adjNotes} onChange={setAdjNotes} />
        <div className="actions left">
          <button className="primary" onClick={adjustStock}>Adjust Stock</button>
        </div>
      </div>

      {movements.length > 0 && (
        <div className="table-shell">
          <table>
            <thead>
              <tr>
                <th>Type</th>
                <th>Quantity</th>
                <th>Unit Cost</th>
                <th>Reason</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              {movements.map((m) => (
                <tr key={m.id}>
                  <td>{m.type}</td>
                  <td>{m.quantity}</td>
                  <td>{m.unitCost?.toFixed(2) ?? "-"}</td>
                  <td>{m.reason || "-"}</td>
                  <td>{new Date(m.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
