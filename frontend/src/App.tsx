import { useEffect, useState } from "react";

type ApiStatus = "checking" | "online" | "offline";

export default function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>("checking");

  useEffect(() => {
    const controller = new AbortController();

    fetch("/api/health", { signal: controller.signal })
      .then((response) => {
        if (!response.ok) {
          throw new Error(`API request failed with status ${response.status}`);
        }
        return response.json();
      })
      .then(() => setApiStatus("online"))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") {
          return;
        }
        setApiStatus("offline");
        console.error("Impossible de joindre l’API Malawi Resto.", error);
      });

    return () => controller.abort();
  }, []);

  const statusLabel = {
    checking: "Vérification de la connexion…",
    online: "API connectée",
    offline: "API indisponible",
  }[apiStatus];

  return (
    <main className="page">
      <section className="welcome-card" aria-labelledby="welcome-title">
        <p className="eyebrow">Restaurant MALAWI</p>
        <h1 id="welcome-title">Bienvenue</h1>
        <p className="description">
          La nouvelle interface de gestion des commandes est en préparation.
        </p>
        <p className={`api-status api-status--${apiStatus}`} role="status">
          <span className="status-dot" aria-hidden="true" />
          {statusLabel}
        </p>
      </section>
    </main>
  );
}
