import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from "react";

type WorkspaceView =
  | "dashboard"
  | "orders"
  | "clients"
  | "users"
  | "deliveries"
  | "payments"
  | "reports"
  | "history";

type Order = {
  id: number;
  date: string;
  montantTotal: number;
  devise: string;
  statut: string;
  modeService: string;
  adresseLivraison: string | null;
  idClient: number;
};

type Client = {
  id: number;
  nom: string;
  prenom: string;
  telephone: string | null;
  adresse: string | null;
  email: string | null;
};

type MenuProduct = {
  id: number;
  name: string;
  price: number;
  isAvailable: boolean;
  productType: "Plat" | "Boisson";
  quantity: number;
};

type UserAccount = {
  id: number;
  nom: string;
  prenom: string;
  email: string;
  role: string;
  actif: boolean;
  dateCreation: string;
};

type AvailableCourier = {
  id: number;
  nom: string;
  prenom: string;
};

type Delivery = {
  id: number;
  idCommande: number;
  idLivreur: number;
  dateDebut: string;
  dateLivree: string | null;
  statut: string;
  adresse: string | null;
  telephoneClient: string | null;
  client: string;
};

type Payment = {
  id: number;
  date: string;
  montant: number;
  devise: string;
  modePaiement: string;
  referenceTransaction: string | null;
  statut: string;
  idCommande: number;
};

type SalesReport = {
  du: string;
  au: string;
  ventesParJour: { date: string; devise: string; total: number; nombreCommandes: number }[];
  produitsLesPlusVendus: { nom: string; type: string; quantiteVendue: number }[];
};

type Operation = {
  id: number;
  idUtilisateur: number | null;
  email: string | null;
  type: string;
  entite: string;
  idEntite: string | null;
  details: string | null;
  date: string;
};

type WorkspaceProps = {
  view: WorkspaceView;
  userId: number;
  role: string;
};

async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...init, headers, credentials: "same-origin" });
  if (!response.ok) {
    let message = `La requête a échoué (${response.status}).`;
    try {
      const problem = (await response.json()) as { title?: string; detail?: string };
      message = problem.title ?? problem.detail ?? message;
    } catch {
      // Some non-JSON error responses have no readable problem details.
    }
    throw new Error(message);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

const money = (amount: number, currency = "HTG") =>
  new Intl.NumberFormat("fr-HT", { style: "currency", currency }).format(amount);

const dateTime = (value: string) =>
  new Date(value).toLocaleString("fr-HT", { dateStyle: "medium", timeStyle: "short" });

function SectionHeading({ eyebrow, title, action }: { eyebrow: string; title: string; action?: ReactNode }) {
  return (
    <div className="section-heading">
      <div><p className="eyebrow">{eyebrow}</p><h2>{title}</h2></div>
      {action}
    </div>
  );
}

function DataTable({ children }: { children: ReactNode }) {
  return <div className="admin-table-wrap"><table className="admin-table">{children}</table></div>;
}

function PageError({ message, retry }: { message: string | null; retry?: () => void }) {
  if (!message) return null;
  return <div className="alert" role="alert">{message}{retry && <button className="button button--quiet" onClick={retry}>Réessayer</button>}</div>;
}

function useAutoRefresh(load: () => Promise<void>, enabled: boolean, intervalMs = 10000) {
  useEffect(() => {
    if (!enabled) return;

    let timeout: number;
    let active = true;
    const refresh = async () => {
      await load();
      if (active) timeout = window.setTimeout(() => void refresh(), intervalMs);
    };
    timeout = window.setTimeout(() => void refresh(), intervalMs);
    return () => {
      active = false;
      window.clearTimeout(timeout);
    };
  }, [load, enabled, intervalMs]);
}

function Dashboard({ role }: { role: string }) {
  const [orders, setOrders] = useState<Order[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [report, setReport] = useState<SalesReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    const from = new Date();
    from.setDate(from.getDate() - 29);
    const toIsoDate = (date: Date) => {
      const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
      return local.toISOString().slice(0, 10);
    };
    try {
      const tasks = [
        api<Order[]>("/api/orders"),
        api<Payment[]>("/api/payments"),
        api<SalesReport>(`/api/reports/sales?from=${toIsoDate(from)}&to=${toIsoDate(new Date())}`),
      ] as const;
      const results = await Promise.allSettled(tasks);
      const failures = results.filter((result) => result.status === "rejected");
      const [ordersResult, paymentsResult, reportResult] = results;
      if (ordersResult.status === "fulfilled") setOrders(ordersResult.value);
      if (paymentsResult.status === "fulfilled") setPayments(paymentsResult.value);
      if (reportResult.status === "fulfilled") setReport(reportResult.value);
      if (failures.length) {
        setError("Certaines données du tableau de bord n’ont pas pu être chargées. Les sections disponibles restent affichées.");
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Le tableau de bord est indisponible.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);
  useAutoRefresh(load, role === "Superviseur");

  const pending = orders.filter((order) =>
    ["En attente de paiement", "Confirmée", "En préparation", "Prête", "En livraison"].includes(order.statut),
  ).length;
  const paidTotals = payments
    .filter((payment) => payment.statut === "Réussi")
    .reduce<Record<string, number>>((totals, payment) => {
      totals[payment.devise] = (totals[payment.devise] ?? 0) + payment.montant;
      return totals;
    }, {});

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Vue d’ensemble" title="Tableau de bord" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      <div className="stat-grid">
        <article className="stat-card"><span>Commandes en cours</span><strong>{loading ? "…" : pending}</strong><small>Demandes à traiter</small></article>
        <article className="stat-card"><span>Commandes reçues</span><strong>{loading ? "…" : orders.length}</strong><small>Dans la liste accessible à votre rôle</small></article>
        <article className="stat-card"><span>Ventes confirmées</span><strong>{loading ? "…" : Object.keys(paidTotals).length ? Object.entries(paidTotals).map(([currency, total]) => money(total, currency)).join(" · ") : "—"}</strong><small>{Object.keys(paidTotals).length ? "Paiements réussis, par devise" : "Aucun paiement confirmé"}</small></article>
        <article className="stat-card"><span>Équipe connectée</span><strong>{role}</strong><small>Accès selon votre rôle</small></article>
      </div>
      <div className="workspace-grid">
        <section className="workspace-card workspace-card--wide">
          <div className="workspace-card__heading"><div><p className="eyebrow">Activité récente</p><h3>Dernières commandes</h3></div></div>
          {orders.length === 0 ? <p className="empty-state">{loading ? "Chargement…" : "Aucune commande à afficher."}</p> : (
            <DataTable><thead><tr><th>N°</th><th>Date</th><th>Service</th><th>Statut</th><th>Total</th></tr></thead>
              <tbody>{orders.slice(0, 6).map((order) => <tr key={order.id}><td>#{order.id}</td><td>{dateTime(order.date)}</td><td>{order.modeService}</td><td><span className="status-label">{order.statut}</span></td><td>{money(order.montantTotal, order.devise)}</td></tr>)}</tbody>
            </DataTable>
          )}
        </section>
        <section className="workspace-card">
          <div className="workspace-card__heading"><div><p className="eyebrow">Indicateurs</p><h3>Meilleures ventes</h3></div></div>
          {!report?.produitsLesPlusVendus.length ? <p className="muted">{loading ? "Chargement…" : "Aucune vente confirmée sur la période."}</p> : (
            <ol className="top-products">{report.produitsLesPlusVendus.slice(0, 5).map((product) => <li key={`${product.type}-${product.nom}`}><span>{product.nom}<small>{product.type}</small></span><strong>{product.quantiteVendue}</strong></li>)}</ol>
          )}
        </section>
      </div>
    </section>
  );
}

function OrdersPage({ role }: { role: string }) {
  const [orders, setOrders] = useState<Order[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [products, setProducts] = useState<MenuProduct[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [creating, setCreating] = useState(false);
  const [clientId, setClientId] = useState("");
  const [serviceMode, setServiceMode] = useState<"Retrait" | "Livraison">("Retrait");
  const [address, setAddress] = useState("");
  const [lines, setLines] = useState([{ idProduit: "", quantite: 1 }]);
  const canManageOrders = role === "Administrateur" || role === "Caissier";

  const load = useCallback(async () => {
    try {
      setOrders(await api<Order[]>("/api/orders"));
      if (canManageOrders) {
        const results = await Promise.allSettled([
          api<Client[]>("/api/clients"),
          api<MenuProduct[]>("/api/menu"),
        ]);
        const [clientResult, productResult] = results;
        if (clientResult.status === "fulfilled") setClients(clientResult.value);
        if (productResult.status === "fulfilled") setProducts(productResult.value.filter((product) => product.isAvailable && product.quantity > 0));
        const failed = results.find((result) => result.status === "rejected");
        if (failed?.status === "rejected") {
          throw failed.reason;
        }
      }
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Certaines données n’ont pas pu être chargées.");
    }
  }, [canManageOrders]);
  useEffect(() => { void load(); }, [load]);
  useAutoRefresh(load, role === "Superviseur");

  function nextStatuses(order: Order): string[] {
    if (order.statut === "Confirmée") return ["En préparation"];
    if (order.statut === "En préparation") return ["Prête", ...(role === "Administrateur" ? ["Annulée"] : [])];
    if (order.statut === "Prête" && order.modeService === "Retrait") return ["Retirée au restaurant"];
    return [];
  }

  async function changeStatus(order: Order, statut: string) {
    setBusyId(order.id);
    setError(null);
    try {
      await api(`/api/orders/${order.id}/status`, { method: "PATCH", body: JSON.stringify({ statut }) });
      await load();
    } catch (changeError) {
      setError(changeError instanceof Error ? changeError.message : "Le statut n’a pas pu être modifié.");
    } finally { setBusyId(null); }
  }

  async function createOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setCreating(true);
    setError(null);
    try {
      await api("/api/orders", {
        method: "POST",
        body: JSON.stringify({
          idClient: Number(clientId),
          modeService: serviceMode,
          adresseLivraison: serviceMode === "Livraison" ? address : null,
          lignes: lines.map((line) => ({
            idProduit: Number(line.idProduit),
            quantite: Number(line.quantite),
          })),
        }),
      });
      setClientId("");
      setAddress("");
      setServiceMode("Retrait");
      setLines([{ idProduit: "", quantite: 1 }]);
      await load();
      setError(null);
    } catch (createError) {
      setError(createError instanceof Error ? createError.message : "La commande n’a pas pu être enregistrée.");
    } finally {
      setCreating(false);
    }
  }

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Suivi opérationnel" title="Commandes" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      {role === "Superviseur" && <p className="fine-print">Actualisation automatique toutes les 10 secondes · consultation seule.</p>}
      {canManageOrders && <form className="editor-card" onSubmit={(event) => void createOrder(event)}>
        <h3>Nouvelle commande client</h3>
        <div className="form-grid">
          <label>Client<select required value={clientId} onChange={(event) => setClientId(event.target.value)}><option value="">Sélectionner un client</option>{clients.map((client) => <option key={client.id} value={client.id}>{client.prenom} {client.nom} · #{client.id}</option>)}</select></label>
          <label>Mode de service<select value={serviceMode} onChange={(event) => setServiceMode(event.target.value as "Retrait" | "Livraison")}><option value="Retrait">Retrait au restaurant</option><option value="Livraison">Livraison</option></select></label>
          {serviceMode === "Livraison" && <label className="wide-field">Adresse de livraison<input required maxLength={500} value={address} onChange={(event) => setAddress(event.target.value)} /></label>}
          {lines.map((line, index) => <div className="order-line-editor wide-field" key={index}>
            <label>Produit<select required value={line.idProduit} onChange={(event) => setLines((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, idProduit: event.target.value } : row))}><option value="">Sélectionner un produit</option>{products.map((product) => <option key={product.id} value={product.id}>{product.name} · {product.productType} · {money(product.price)}</option>)}</select></label>
            <label>Quantité<input type="number" required min="1" max={products.find((product) => String(product.id) === line.idProduit)?.quantity ?? 1000} value={line.quantite} onChange={(event) => setLines((current) => current.map((row, rowIndex) => rowIndex === index ? { ...row, quantite: Number(event.target.value) } : row))} /></label>
            {lines.length > 1 && <button type="button" className="button button--quiet remove-line" aria-label={`Retirer la ligne ${index + 1}`} onClick={() => setLines((current) => current.filter((_, rowIndex) => rowIndex !== index))}>Retirer</button>}
          </div>)}
        </div>
        <div className="form-actions">
          <button type="button" className="button button--quiet" onClick={() => setLines((current) => [...current, { idProduit: "", quantite: 1 }])}>Ajouter un produit</button>
          <button className="button button--accent" disabled={creating || !clients.length || !products.length}>{creating ? "Enregistrement…" : "Créer la commande"}</button>
        </div>
        <p className="fine-print">Le montant est calculé par l’API à partir des prix en base. La commande reste en attente de paiement.</p>
      </form>}
      {orders.length === 0 ? <p className="empty-state">Aucune commande à afficher.</p> : (
        <DataTable><thead><tr><th>N°</th><th>Date</th><th>Client</th><th>Service</th><th>Statut</th><th>Total</th>{canManageOrders && <th>Action</th>}</tr></thead>
          <tbody>{orders.map((order) => <tr key={order.id}><td>#{order.id}</td><td>{dateTime(order.date)}</td><td>Client #{order.idClient}</td><td>{order.modeService}{order.adresseLivraison && <small className="table-subline">{order.adresseLivraison}</small>}</td><td><span className="status-label">{order.statut}</span></td><td>{money(order.montantTotal, order.devise)}</td>{canManageOrders && <td>
            {nextStatuses(order).map((status) => <button className="button button--quiet table-action" key={status} disabled={busyId === order.id} onClick={() => void changeStatus(order, status)}>{status}</button>)}
            {!nextStatuses(order).length && <span className="muted">—</span>}
          </td>}</tr>)}</tbody>
        </DataTable>
      )}
    </section>
  );
}

function ClientsPage() {
  const [clients, setClients] = useState<Client[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ nom: "", prenom: "", telephone: "", adresse: "", email: "" });
  const load = useCallback(async () => {
    try { setClients(await api<Client[]>("/api/clients")); setError(null); }
    catch (loadError) { setError(loadError instanceof Error ? loadError.message : "Impossible de charger les clients."); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  function editClient(client: Client) {
    setEditingId(client.id);
    setForm({
      nom: client.nom,
      prenom: client.prenom,
      telephone: client.telephone ?? "",
      adresse: client.adresse ?? "",
      email: client.email ?? "",
    });
  }

  function resetForm() {
    setEditingId(null);
    setForm({ nom: "", prenom: "", telephone: "", adresse: "", email: "" });
  }

  async function saveClient(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await api(editingId ? `/api/clients/${editingId}` : "/api/clients", {
        method: editingId ? "PUT" : "POST",
        body: JSON.stringify({
          ...form,
          telephone: form.telephone || null,
          adresse: form.adresse || null,
          email: form.email || null,
        }),
      });
      resetForm();
      await load();
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "Le client n’a pas pu être enregistré."); }
    finally { setSaving(false); }
  }

  const filteredClients = clients.filter((client) =>
    `${client.prenom} ${client.nom} ${client.email ?? ""} ${client.telephone ?? ""}`
      .toLocaleLowerCase("fr-HT")
      .includes(query.trim().toLocaleLowerCase("fr-HT")),
  );

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Relations clientèle" title="Clients" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      <div className="stat-grid stat-grid--compact"><article className="stat-card"><span>Clients enregistrés</span><strong>{clients.length}</strong><small>Profils visibles pour votre rôle</small></article></div>
      <form className="editor-card" onSubmit={(event) => void saveClient(event)}>
        <h3>{editingId ? "Modifier le client" : "Ajouter un client"}</h3>
        <div className="form-grid">
          <label>Nom<input required maxLength={100} value={form.nom} onChange={(event) => setForm({ ...form, nom: event.target.value })} /></label>
          <label>Prénom<input required maxLength={100} value={form.prenom} onChange={(event) => setForm({ ...form, prenom: event.target.value })} /></label>
          <label>Courriel<input type="email" maxLength={254} value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} /></label>
          <label>Téléphone<input type="tel" maxLength={30} value={form.telephone} onChange={(event) => setForm({ ...form, telephone: event.target.value })} /></label>
          <label className="wide-field">Adresse<input maxLength={500} value={form.adresse} onChange={(event) => setForm({ ...form, adresse: event.target.value })} /></label>
        </div>
        <div className="form-actions"><button className="button button--accent" disabled={saving}>{saving ? "Enregistrement…" : editingId ? "Enregistrer les modifications" : "Ajouter le client"}</button>{editingId && <button type="button" className="button button--quiet" onClick={resetForm}>Annuler</button>}</div>
      </form>
      <label className="client-search">Rechercher un client<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Nom, courriel ou téléphone" /></label>
      {!filteredClients.length ? <p className="empty-state">{clients.length ? "Aucun client ne correspond à votre recherche." : "Aucun client à afficher."}</p> : (
        <DataTable><thead><tr><th>Nom</th><th>Courriel</th><th>Téléphone</th><th>Adresse</th><th>Action</th></tr></thead>
          <tbody>{filteredClients.map((client) => <tr key={client.id}><td><strong>{client.prenom} {client.nom}</strong></td><td>{client.email || "—"}</td><td>{client.telephone || "—"}</td><td>{client.adresse || "—"}</td><td><button className="button button--quiet table-action" onClick={() => editClient(client)}>Modifier</button></td></tr>)}</tbody>
        </DataTable>
      )}
    </section>
  );
}

function UsersPage({ currentUserId }: { currentUserId: number }) {
  const [users, setUsers] = useState<UserAccount[]>([]);
  const [roles, setRoles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({ nom: "", prenom: "", email: "", motDePasse: "", role: "" });

  const load = useCallback(async () => {
    try {
      const [accounts, availableRoles] = await Promise.all([
        api<UserAccount[]>("/api/users"),
        api<string[]>("/api/roles"),
      ]);
      setUsers(accounts);
      const employeeRoles = availableRoles.filter((role) => role !== "Client");
      setRoles(employeeRoles);
      setForm((current) => ({
        ...current,
        role: employeeRoles.includes(current.role) ? current.role : employeeRoles[0] || "",
      }));
      setError(null);
    } catch (loadError) { setError(loadError instanceof Error ? loadError.message : "Impossible de charger les utilisateurs."); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  async function createUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api("/api/users", { method: "POST", body: JSON.stringify(form) });
      setForm({ nom: "", prenom: "", email: "", motDePasse: "", role: roles[0] ?? "" });
      await load();
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "Le compte n’a pas pu être créé."); }
    finally { setBusy(false); }
  }

  async function toggleActive(account: UserAccount) {
    setError(null);
    try {
      await api(`/api/users/${account.id}`, {
        method: "PUT",
        body: JSON.stringify({ nom: account.nom, prenom: account.prenom, role: account.role, actif: !account.actif }),
      });
      await load();
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "Le compte n’a pas pu être modifié."); }
  }

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Administration" title="Utilisateurs" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      <form className="editor-card" onSubmit={(event) => void createUser(event)}>
        <h3>Créer un compte employé</h3>
        <div className="form-grid">
          <label>Nom<input required maxLength={100} value={form.nom} onChange={(event) => setForm({ ...form, nom: event.target.value })} /></label>
          <label>Prénom<input required maxLength={100} value={form.prenom} onChange={(event) => setForm({ ...form, prenom: event.target.value })} /></label>
          <label>Courriel<input type="email" required maxLength={254} value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} /></label>
          <label>Rôle<select required value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })}>{roles.map((role) => <option key={role}>{role}</option>)}</select></label>
          <label className="wide-field">Mot de passe temporaire<input type="password" required minLength={8} maxLength={128} value={form.motDePasse} onChange={(event) => setForm({ ...form, motDePasse: event.target.value })} /><small className="field-hint">8 caractères minimum.</small></label>
        </div>
        <div className="form-actions"><button className="button button--accent" disabled={busy || !roles.length}>{busy ? "Création…" : "Créer l’utilisateur"}</button></div>
      </form>
      {!users.length ? <p className="empty-state">Aucun compte utilisateur à afficher.</p> : (
        <DataTable><thead><tr><th>Nom</th><th>Courriel</th><th>Rôle</th><th>Créé le</th><th>État</th><th>Action</th></tr></thead>
          <tbody>{users.map((account) => <tr key={account.id}><td><strong>{account.prenom} {account.nom}</strong></td><td>{account.email}</td><td>{account.role}</td><td>{new Date(account.dateCreation).toLocaleDateString("fr-HT")}</td><td><span className={account.actif ? "status-label status-label--success" : "status-label"}>{account.actif ? "Actif" : "Inactif"}</span></td><td><button className="button button--quiet table-action" disabled={account.id === currentUserId} onClick={() => void toggleActive(account)}>{account.id === currentUserId ? "Compte actuel" : account.actif ? "Désactiver" : "Réactiver"}</button></td></tr>)}</tbody>
        </DataTable>
      )}
    </section>
  );
}

function DeliveriesPage({ role }: { role: string }) {
  const [deliveries, setDeliveries] = useState<Delivery[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [couriers, setCouriers] = useState<AvailableCourier[]>([]);
  const [orderId, setOrderId] = useState("");
  const [courierId, setCourierId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const canAssign = role === "Administrateur" || role === "Superviseur";

  const load = useCallback(async () => {
    setError(null);
    try {
      const deliveryRows = await api<Delivery[]>("/api/deliveries");
      setDeliveries(deliveryRows);
      if (canAssign) {
        const [orderRows, availableCouriers] = await Promise.all([
          api<Order[]>("/api/orders"),
          api<AvailableCourier[]>("/api/deliveries/couriers"),
        ]);
        const readyOrders = orderRows.filter((order) => order.statut === "Prête" && order.modeService === "Livraison" && !deliveryRows.some((delivery) => delivery.idCommande === order.id));
        setOrders(readyOrders);
        setCouriers(availableCouriers);
        setOrderId((current) => readyOrders.some((order) => String(order.id) === current) ? current : "");
        setCourierId((current) => availableCouriers.some((courier) => String(courier.id) === current) ? current : "");
      }
    } catch (loadError) { setError(loadError instanceof Error ? loadError.message : "Impossible de charger les livraisons."); }
  }, [canAssign]);
  useEffect(() => { void load(); }, [load]);
  useAutoRefresh(load, role === "Superviseur");

  async function assign(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api("/api/deliveries", { method: "POST", body: JSON.stringify({ idCommande: Number(orderId), idLivreur: Number(courierId) }) });
      await load();
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "La livraison n’a pas pu être assignée."); }
    finally { setBusy(false); }
  }

  async function complete(delivery: Delivery) {
    setBusy(true);
    setError(null);
    try {
      await api(`/api/deliveries/${delivery.id}/status`, { method: "PATCH", body: JSON.stringify({ statut: "Livrée" }) });
      await load();
    } catch (saveError) { setError(saveError instanceof Error ? saveError.message : "La livraison n’a pas pu être mise à jour."); }
    finally { setBusy(false); }
  }

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Logistique" title="Livraisons" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      {role === "Superviseur" && <p className="fine-print">Actualisation automatique toutes les 10 secondes.</p>}
      {canAssign && <form className="editor-card inline-form delivery-form" onSubmit={(event) => void assign(event)}>
        <label>Commande prête<select required value={orderId} onChange={(event) => setOrderId(event.target.value)}><option value="">Sélectionner une commande</option>{orders.map((order) => <option key={order.id} value={order.id}>#{order.id} · {money(order.montantTotal, order.devise)}</option>)}</select></label>
        <label>Livreur disponible<select required value={courierId} onChange={(event) => setCourierId(event.target.value)}><option value="">Sélectionner un livreur</option>{couriers.map((courier) => <option key={courier.id} value={courier.id}>{courier.prenom} {courier.nom}</option>)}</select></label>
        <button className="button button--accent" disabled={busy || !orderId || !courierId}>Assigner</button>
        {!orders.length && <p className="fine-print">Aucune commande prête à assigner pour le moment.</p>}
      </form>}
      <div className="stat-grid stat-grid--compact"><article className="stat-card"><span>Livraisons en cours</span><strong>{deliveries.filter((delivery) => delivery.statut === "En cours").length}</strong><small>Sur les livraisons visibles</small></article><article className="stat-card"><span>Livrées</span><strong>{deliveries.filter((delivery) => delivery.statut === "Livrée").length}</strong><small>Livraisons terminées</small></article></div>
      {!deliveries.length ? <p className="empty-state">Aucune livraison à afficher.</p> : (
        <DataTable><thead><tr><th>Commande</th><th>Client</th><th>Adresse</th><th>Téléphone</th><th>Début</th><th>Statut</th><th>Action</th></tr></thead>
          <tbody>{deliveries.map((delivery) => <tr key={delivery.id}><td>#{delivery.idCommande}</td><td>{delivery.client}</td><td>{delivery.adresse || "—"}</td><td>{delivery.telephoneClient || "—"}</td><td>{dateTime(delivery.dateDebut)}</td><td><span className="status-label">{delivery.statut}</span></td><td>{delivery.statut === "En cours" && (role === "Administrateur" || role === "Livreur") ? <button className="button button--quiet table-action" disabled={busy} onClick={() => void complete(delivery)}>Marquer livrée</button> : "—"}</td></tr>)}</tbody>
        </DataTable>
      )}
    </section>
  );
}

function PaymentsPage() {
  const [payments, setPayments] = useState<Payment[]>([]);
  const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => {
    try { setPayments(await api<Payment[]>("/api/payments")); setError(null); }
    catch (loadError) { setError(loadError instanceof Error ? loadError.message : "Impossible de charger les paiements."); }
  }, []);
  useEffect(() => { void load(); }, [load]);

  const totals = payments.reduce<Record<string, number>>((result, payment) => {
    if (payment.statut === "Réussi") result[payment.devise] = (result[payment.devise] ?? 0) + payment.montant;
    return result;
  }, {});

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Suivi financier" title="Paiements" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      <div className="stat-grid stat-grid--compact"><article className="stat-card"><span>Transactions affichées</span><strong>{payments.length}</strong><small>Historique récent</small></article>{Object.entries(totals).map(([currency, total]) => <article className="stat-card" key={currency}><span>Total confirmé · {currency}</span><strong>{money(total, currency)}</strong><small>Paiements réussis</small></article>)}</div>
      {!payments.length ? <p className="empty-state">Aucun paiement enregistré.</p> : (
        <DataTable><thead><tr><th>Transaction</th><th>Commande</th><th>Date</th><th>Mode</th><th>Référence</th><th>Statut</th><th>Montant</th></tr></thead>
          <tbody>{payments.map((payment) => <tr key={payment.id}><td>#{payment.id}</td><td>#{payment.idCommande}</td><td>{dateTime(payment.date)}</td><td>{payment.modePaiement}</td><td>{payment.referenceTransaction || "—"}</td><td><span className="status-label">{payment.statut}</span></td><td>{money(payment.montant, payment.devise)}</td></tr>)}</tbody>
        </DataTable>
      )}
      <p className="fine-print">Consultation uniquement : le paiement en ligne n’est pas encore connecté à MonCash ou NatCash. Aucun paiement ne peut être initié depuis cette interface.</p>
    </section>
  );
}

function ReportsPage() {
  const today = new Date();
  const initialFrom = new Date(today);
  initialFrom.setDate(initialFrom.getDate() - 29);
  const isoDate = (date: Date) => {
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
    return local.toISOString().slice(0, 10);
  };
  const [range, setRange] = useState(() => ({ from: isoDate(initialFrom), to: isoDate(today) }));
  const [appliedRange, setAppliedRange] = useState(range);
  const [report, setReport] = useState<SalesReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async (start: string, end: string) => {
    setBusy(true);
    setError(null);
    try { setReport(await api<SalesReport>(`/api/reports/sales?from=${encodeURIComponent(start)}&to=${encodeURIComponent(end)}`)); }
    catch (loadError) { setError(loadError instanceof Error ? loadError.message : "Impossible de générer le rapport."); }
    finally { setBusy(false); }
  }, []);
  useEffect(() => { void load(appliedRange.from, appliedRange.to); }, [load, appliedRange]);

  const totals = (report?.ventesParJour ?? []).reduce<Record<string, number>>((result, row) => {
    result[row.devise] = (result[row.devise] ?? 0) + row.total;
    return result;
  }, {});

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Analyse d’activité" title="Rapports & statistiques" action={<button className="button button--quiet" onClick={() => void load(appliedRange.from, appliedRange.to)} disabled={busy}>Actualiser</button>} />
      <form className="editor-card inline-form report-filters" onSubmit={(event) => { event.preventDefault(); setAppliedRange(range); }}>
        <label>Du<input type="date" required value={range.from} onChange={(event) => setRange({ ...range, from: event.target.value })} /></label>
        <label>Au<input type="date" required value={range.to} onChange={(event) => setRange({ ...range, to: event.target.value })} /></label>
        <button className="button button--accent" disabled={busy}>{busy ? "Chargement…" : "Appliquer"}</button>
        <small className="field-hint">La période ne peut pas dépasser 366 jours.</small>
      </form>
      <PageError message={error} />
      <div className="stat-grid stat-grid--compact">{Object.entries(totals).length ? Object.entries(totals).map(([currency, amount]) => <article className="stat-card" key={currency}><span>Ventes confirmées · {currency}</span><strong>{money(amount, currency)}</strong><small>Pour la période sélectionnée</small></article>) : <article className="stat-card"><span>Ventes confirmées</span><strong>—</strong><small>Aucune vente sur la période</small></article>}<article className="stat-card"><span>Commandes payées</span><strong>{report?.ventesParJour.reduce((count, row) => count + row.nombreCommandes, 0) ?? "—"}</strong><small>Nombre quotidien cumulé</small></article></div>
      <div className="workspace-grid">
        <section className="workspace-card workspace-card--wide"><div className="workspace-card__heading"><div><p className="eyebrow">Paiements réussis</p><h3>Ventes par jour</h3></div></div>
          {!report?.ventesParJour.length ? <p className="empty-state">Aucune vente confirmée pour cette période.</p> : <DataTable><thead><tr><th>Date</th><th>Commandes</th><th>Devise</th><th>Ventes</th></tr></thead><tbody>{report.ventesParJour.map((row) => <tr key={`${row.date}-${row.devise}`}><td>{new Date(row.date).toLocaleDateString("fr-HT")}</td><td>{row.nombreCommandes}</td><td>{row.devise}</td><td>{money(row.total, row.devise)}</td></tr>)}</tbody></DataTable>}
        </section>
        <section className="workspace-card"><div className="workspace-card__heading"><div><p className="eyebrow">Meilleures ventes</p><h3>Produits populaires</h3></div></div>
          {!report?.produitsLesPlusVendus.length ? <p className="muted">Pas de ventes produits confirmées pour la période.</p> : <ol className="top-products">{report.produitsLesPlusVendus.map((product) => <li key={`${product.type}-${product.nom}`}><span>{product.nom}<small>{product.type}</small></span><strong>{product.quantiteVendue}</strong></li>)}</ol>}
        </section>
      </div>
    </section>
  );
}

function HistoryPage() {
  const [operations, setOperations] = useState<Operation[]>([]);
  const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => {
    try {
      setOperations(await api<Operation[]>("/api/history?limit=100"));
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Impossible de charger l’historique.");
    }
  }, []);
  useEffect(() => { void load(); }, [load]);

  return (
    <section className="dashboard-section">
      <SectionHeading eyebrow="Traçabilité" title="Historique des opérations" action={<button className="button button--quiet" onClick={() => void load()}>Actualiser</button>} />
      <PageError message={error} />
      {!operations.length ? <p className="empty-state">{error ? "L’historique est indisponible." : "Aucune opération à afficher."}</p> : (
        <DataTable><thead><tr><th>Date</th><th>Opération</th><th>Entité</th><th>Identifiant</th><th>Utilisateur</th><th>Détails</th></tr></thead>
          <tbody>{operations.map((operation) => <tr key={operation.id}>
            <td>{dateTime(operation.date)}</td>
            <td>{operation.type}</td>
            <td>{operation.entite}</td>
            <td>{operation.idEntite ?? "—"}</td>
            <td>{operation.email ?? (operation.idUtilisateur ? `Utilisateur #${operation.idUtilisateur}` : "Système")}</td>
            <td>{operation.details ?? "—"}</td>
          </tr>)}</tbody>
        </DataTable>
      )}
    </section>
  );
}

export default function AdminWorkspace({ view, userId, role }: WorkspaceProps) {
  switch (view) {
    case "dashboard": return <Dashboard role={role} />;
    case "orders": return <OrdersPage role={role} />;
    case "clients": return <ClientsPage />;
    case "users": return <UsersPage currentUserId={userId} />;
    case "deliveries": return <DeliveriesPage role={role} />;
    case "payments": return <PaymentsPage />;
    case "reports": return <ReportsPage />;
    case "history": return <HistoryPage />;
  }
}
