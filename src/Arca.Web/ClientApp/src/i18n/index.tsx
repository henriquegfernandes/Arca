import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import enUS from "./locales/en-US.json";
import ptBR from "./locales/pt-BR.json";

type Language = "en-US" | "pt-BR";
type Dictionary = Record<string, string>;

const dictionaries: Record<Language, Dictionary> = {
  "en-US": enUS,
  "pt-BR": ptBR,
};

const LanguageContext = createContext<{
  language: Language;
  setLanguage: (language: Language) => void;
  t: (key: string) => string;
} | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(resolveLanguage);

  const value = useMemo(() => ({
    language,
    setLanguage: (nextLanguage: Language) => {
      localStorage.setItem("arca.language", nextLanguage);
      setLanguageState(nextLanguage);
    },
    t: (key: string) => dictionaries[language][key] ?? dictionaries["en-US"][key] ?? key,
  }), [language]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useI18n() {
  const context = useContext(LanguageContext);
  if (!context) throw new Error("useI18n must be used within LanguageProvider.");
  return context;
}

function resolveLanguage(): Language {
  const saved = localStorage.getItem("arca.language");
  if (saved === "pt-BR" || saved === "en-US") return saved;
  return navigator.language?.toLowerCase().startsWith("pt") ? "pt-BR" : "en-US";
}
