export function Field({
  label,
  value,
  onChange,
  error,
  required,
  type = "text",
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  required?: boolean;
  type?: "text" | "email" | "password";
}) {
  return (
    <label className="field">
      <span>
        {label}
        {required ? " *" : ""}
      </span>
      <input
        type={type}
        value={value}
        required={required}
        aria-invalid={error ? "true" : "false"}
        onChange={(event) => onChange(event.target.value)}
      />
      {error && <small className="field-error">{error}</small>}
    </label>
  );
}
