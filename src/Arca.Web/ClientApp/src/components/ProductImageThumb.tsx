import { useState } from "react";
import { useI18n } from "../i18n";

export function ProductImageThumb({
  src,
  alt,
  size = "md",
  className = "",
}: {
  src?: string | null;
  alt: string;
  size?: "sm" | "md" | "lg";
  className?: string;
}) {
  const { t } = useI18n();
  const [failed, setFailed] = useState(false);
  const canRenderImage = src && !failed;

  return (
    <div className={`product-thumb ${size} ${className}`.trim()}>
      {canRenderImage ? (
        <img src={src} alt={alt} onError={() => setFailed(true)} />
      ) : (
        <span>{t("common.noImage")}</span>
      )}
    </div>
  );
}
