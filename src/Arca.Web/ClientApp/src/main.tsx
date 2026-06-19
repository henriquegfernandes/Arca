import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
import { AppContextProvider } from "./context/AppContext";
import { LanguageProvider } from "./i18n";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <LanguageProvider>
      <AppContextProvider>
        <App />
      </AppContextProvider>
    </LanguageProvider>
  </StrictMode>
);
