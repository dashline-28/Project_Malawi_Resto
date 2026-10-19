import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import AdminWorkspace from "./AdminWorkspace";

type Product = {
  id: number;
  name: string;
  description: string | null;
  price: number;
  category: string;
  isAvailable: boolean;
  productType: "Plat" | "Boisson";
  quantity: number;
  isEnabled: boolean;
};

type Category = { id: number; nom: string; active: boolean };
type User = { id: number; nom: string; prenom: string; email: string; role: string };
type ClientProfile = {
  id: number;
  nom: string;
  prenom: string;
  telephone: string | null;
  adresse: string | null;
  email: string | null;
};
type Order = {
  id: number;
  date: string;
  montantTotal: number;
  devise: string;
  statut: string;
  modeService: string;
};
type CartLine = { product: Product; quantity: number };
type View =
  | "menu"
  | "clientHome"
  | "orders"
  | "profile"
  | "products"
  | "categories"
  | "dashboard"
  | "manageOrders"
  | "clients"
  | "users"
  | "deliveries"
  | "payments"
  | "reports"
  | "history";

function defaultViewForRole(role: string): View {
  if (role === "Directeur") return "reports";
  if (["Administrateur", "Caissier", "Superviseur"].includes(role)) return "dashboard";
  if (role === "Livreur") return "deliveries";
  if (role === "Client") return "clientHome";
  return "menu";
}

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

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

const money = (amount: number) =>
  new Intl.NumberFormat("fr-HT", { style: "currency", currency: "HTG" }).format(amount);

export default function App({ staffOnly = false }: { staffOnly?: boolean }) {
  const [items, setItems] = useState<Product[]>([]);
  const [user, setUser] = useState<User | null>(null);
  const [view, setView] = useState<View>(staffOnly ? "dashboard" : "menu");
  const [cart, setCart] = useState<CartLine[]>([]);
  const [pageError, setPageError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loadingMenu, setLoadingMenu] = useState(true);
  const [serviceMode, setServiceMode] = useState<"Retrait" | "Livraison">("Retrait");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [categories, setCategories] = useState<Category[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [clientProfile, setClientProfile] = useState<ClientProfile | null>(null);
  const [editingProductId, setEditingProductId] = useState<number | null>(null);
  const [productForm, setProductForm] = useState({
    nom: "",
    quantite: 0,
    description: "",
    prix: 0,
    categorie: "",
    typeProduit: "Plat" as Product["productType"],
    disponibilite: true,
  });
  const [categoryName, setCategoryName] = useState("");

  const isAdmin = user?.role === "Administrateur";
  const isCashier = user?.role === "Caissier";
  const isSupervisor = user?.role === "Superviseur";
  const isDirector = user?.role === "Directeur";
  const isCourier = user?.role === "Livreur";
  const isClient = !staffOnly && user?.role === "Client";
  const isCustomerInterface = !staffOnly && (!user || isClient);
  const isStaff = isAdmin || isCashier || isSupervisor || isDirector || isCourier;
  const cartTotal = useMemo(
    () => cart.reduce((total, line) => total + line.product.price * line.quantity, 0),
    [cart],
  );
  const activeCategories = categories.filter((category) => category.active);

  const loadMenu = useCallback(async () => {
    setLoadingMenu(true);
    try {
      setItems(await api<Product[]>("/api/menu"));
      setPageError(null);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "Impossible de charger le menu.");
    } finally {
      setLoadingMenu(false);
    }
  }, []);

  const loadCategories = useCallback(async () => {
    setCategories(await api<Category[]>("/api/categories"));
  }, []);

  const loadOrders = useCallback(async () => {
    setOrders(await api<Order[]>("/api/orders"));
  }, []);

  const loadClientProfile = useCallback(async () => {
    const clients = await api<ClientProfile[]>("/api/clients");
    setClientProfile(clients[0] ?? null);
  }, []);

  useEffect(() => {
    void loadMenu();
    api<User>("/api/auth/me")
      .then((signedIn) => {
        if (staffOnly && signedIn.role === "Client") {
          window.location.replace("/client/");
          return;
        }
        setUser(signedIn);
        setView(defaultViewForRole(signedIn.role));
      })
      .catch((error: unknown) => {
        if (error instanceof Error && !error.message.includes("(401)")) {
          console.error("Impossible de vérifier la session.", error);
        }
      });
  }, [loadMenu, staffOnly]);

  useEffect(() => {
    if (view === "products" || view === "categories") {
      loadCategories().catch((error: unknown) =>
        setPageError(error instanceof Error ? error.message : "Impossible de charger les catégories."),
      );
    }
    if ((view === "orders" || view === "clientHome") && isClient) {
      loadOrders().catch((error: unknown) =>
        setPageError(error instanceof Error ? error.message : "Impossible de charger les commandes."),
      );
    }
    if (view === "profile" && isClient) {
      loadClientProfile().catch((error: unknown) =>
        setPageError(error instanceof Error ? error.message : "Impossible de charger votre profil."),
      );
    }
  }, [view, isClient, loadCategories, loadOrders, loadClientProfile]);

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setPageError(null);
    setNotice(null);
    try {
      if (authMode === "login") {
        const signedIn = await api<User>("/api/auth/login", {
          method: "POST",
          body: JSON.stringify({ email, motDePasse: password }),
        });
        if (staffOnly && signedIn.role === "Client") {
          window.location.assign("/client/");
          return;
        }
        setUser(signedIn);
        setView(defaultViewForRole(signedIn.role));
        setPassword("");
        setNotice(`Bienvenue, ${signedIn.prenom}.`);
      } else {
        const form = new FormData(event.currentTarget);
        await api("/api/auth/register", {
          method: "POST",
          body: JSON.stringify({
            nom: form.get("nom"),
            prenom: form.get("prenom"),
            email,
            motDePasse: password,
            telephone: form.get("telephone") || null,
            adresse: form.get("adresse") || null,
          }),
        });
        setAuthMode("login");
        setNotice("Compte créé. Vous pouvez maintenant vous connecter.");
      }
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "L’authentification a échoué.");
    } finally {
      setBusy(false);
    }
  }

  async function signOut() {
    try {
      await api("/api/auth/logout", { method: "POST" });
      setUser(null);
      setCart([]);
      setView("menu");
      setNotice("Vous êtes déconnecté.");
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "La déconnexion a échoué.");
    }
  }

  function addToCart(product: Product) {
    setCart((current) => {
      const line = current.find((entry) => entry.product.id === product.id);
      return line
        ? current.map((entry) =>
            entry.product.id === product.id
              ? { ...entry, quantity: Math.min(entry.quantity + 1, product.quantity) }
              : entry,
          )
        : [...current, { product, quantity: 1 }];
    });
    setNotice(`${product.name} ajouté à la commande.`);
    setPageError(null);
  }

  function updateCartQuantity(productId: number, quantity: number) {
    setCart((current) =>
      quantity <= 0
        ? current.filter((line) => line.product.id !== productId)
        : current.map((line) =>
            line.product.id === productId
              ? { ...line, quantity: Math.min(quantity, line.product.quantity) }
              : line,
          ),
    );
  }

  async function submitOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setPageError(null);
    try {
      await api("/api/orders", {
        method: "POST",
        body: JSON.stringify({
          modeService: serviceMode,
          adresseLivraison: serviceMode === "Livraison" ? deliveryAddress : null,
          lignes: cart.map(({ product, quantity }) => ({
            idProduit: product.id,
            quantite: quantity,
            typeProduit: product.productType,
          })),
        }),
      });
      setCart([]);
      setNotice("Commande enregistrée. Le paiement en ligne n’est pas encore activé.");
      setView("orders");
      await loadOrders();
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "La commande n’a pas pu être enregistrée.");
    } finally {
      setBusy(false);
    }
  }

  async function loadProductsForAdmin() {
    try {
      setItems(await api<Product[]>("/api/products"));
      setPageError(null);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "Impossible de charger les produits.");
    }
  }

  async function saveProduct(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setPageError(null);
    const payload = {
      nom: productForm.nom,
      quantite: Number(productForm.quantite),
      description: productForm.description || null,
      prix: Number(productForm.prix),
      categorie: productForm.categorie,
      typeProduit: productForm.typeProduit,
      disponibilite: productForm.disponibilite,
    };
    try {
      await api(editingProductId ? `/api/products/${editingProductId}` : "/api/products", {
        method: editingProductId ? "PUT" : "POST",
        body: JSON.stringify(payload),
      });
      setNotice(editingProductId ? "Produit modifié." : "Produit ajouté.");
      resetProductForm();
      await Promise.all([loadProductsForAdmin(), loadMenu()]);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "Le produit n’a pas pu être enregistré.");
    } finally {
      setBusy(false);
    }
  }

  function editProduct(product: Product) {
    setEditingProductId(product.id);
    setProductForm({
      nom: product.name,
      quantite: product.quantity,
      description: product.description ?? "",
      prix: product.price,
      categorie: product.category,
      typeProduit: product.productType,
      disponibilite: product.isEnabled,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function resetProductForm() {
    setEditingProductId(null);
    setProductForm({
      nom: "",
      quantite: 0,
      description: "",
      prix: 0,
      categorie: activeCategories[0]?.nom ?? "",
      typeProduit: "Plat",
      disponibilite: true,
    });
  }

  async function disableProduct(id: number) {
    if (!window.confirm("Rendre ce produit indisponible ?")) return;
    try {
      await api(`/api/products/${id}`, { method: "DELETE" });
      setNotice("Produit rendu indisponible.");
      await Promise.all([loadProductsForAdmin(), loadMenu()]);
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "Le produit n’a pas pu être désactivé.");
    }
  }

  async function createCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await api("/api/categories", {
        method: "POST",
        body: JSON.stringify({ nom: categoryName, active: true }),
      });
      setCategoryName("");
      setNotice("Catégorie créée.");
      await loadCategories();
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "La catégorie n’a pas pu être créée.");
    }
  }

  async function toggleCategory(category: Category) {
    try {
      await api(`/api/categories/${category.id}`, {
        method: "PUT",
        body: JSON.stringify({ nom: category.nom, active: !category.active }),
      });
      setNotice(category.active ? "Catégorie désactivée." : "Catégorie réactivée.");
      await loadCategories();
    } catch (error) {
      setPageError(error instanceof Error ? error.message : "La catégorie n’a pas pu être modifiée.");
    }
  }

  useEffect(() => {
    if (isAdmin && view === "products") {
      void loadProductsForAdmin();
    }
  }, [isAdmin, view]);

  const groupedItems = useMemo(
    () => [...new Set(items.map((item) => item.category))].map((category) => ({
      category,
      products: items.filter((item) => item.category === category),
    })),
    [items],
  );

  return (
    <main className={isCustomerInterface ? "app-shell client-portal" : "app-shell staff-portal"}>
      <header className={isStaff ? "topbar topbar--staff" : isCustomerInterface ? "topbar topbar--client" : "topbar"}>
        <a className="brand" href={staffOnly ? "/staff/" : "/client/"} onClick={(event) => { event.preventDefault(); setView(staffOnly ? defaultViewForRole(user?.role ?? "") : "menu"); }}>
          <img className="brand-logo" src="/logo-malawi.jpg" alt="Logo Malawi Hôtel" />
          <span><strong>MALAWI</strong><small>{staffOnly ? "Espace personnel" : isCustomerInterface ? "Espace client" : "Espace personnel"}</small></span>
        </a>
        <nav className={isStaff ? "main-nav main-nav--staff" : "main-nav"} aria-label="Navigation principale">
          {isClient && <button className={view === "clientHome" ? "nav-link active" : "nav-link"} onClick={() => setView("clientHome")}>Accueil</button>}
          {isCustomerInterface && <button className={view === "menu" ? "nav-link active" : "nav-link"} onClick={() => setView("menu")}>Le menu</button>}
          {isClient && <button className={view === "orders" ? "nav-link active" : "nav-link"} onClick={() => setView("orders")}>Mes commandes</button>}
          {isClient && <button className={view === "profile" ? "nav-link active" : "nav-link"} onClick={() => setView("profile")}>Mon profil</button>}
          {(isAdmin || isCashier || isSupervisor) && <>
            <button className={view === "dashboard" ? "nav-link active" : "nav-link"} onClick={() => setView("dashboard")}>Tableau de bord</button>
            <button className={view === "manageOrders" ? "nav-link active" : "nav-link"} onClick={() => setView("manageOrders")}>Commandes</button>
            {(isAdmin || isCashier) && <button className={view === "clients" ? "nav-link active" : "nav-link"} onClick={() => setView("clients")}>Clients</button>}
            <button className={view === "payments" ? "nav-link active" : "nav-link"} onClick={() => setView("payments")}>Paiements</button>
            <button className={view === "reports" ? "nav-link active" : "nav-link"} onClick={() => setView("reports")}>Rapports</button>
          </>}
          {(isAdmin || isCashier || isCourier || isSupervisor) && <button className={view === "deliveries" ? "nav-link active" : "nav-link"} onClick={() => setView("deliveries")}>Livraisons</button>}
          {(isAdmin || isSupervisor) && <button className={view === "history" ? "nav-link active" : "nav-link"} onClick={() => setView("history")}>Historique</button>}
          {isDirector && <>
            <button className={view === "reports" ? "nav-link active" : "nav-link"} onClick={() => setView("reports")}>Rapports & statistiques</button>
            <button className={view === "history" ? "nav-link active" : "nav-link"} onClick={() => setView("history")}>Historique général</button>
          </>}
          {isAdmin && <>
            <button className={view === "products" ? "nav-link active" : "nav-link"} onClick={() => setView("products")}>Produits</button>
            <button className={view === "categories" ? "nav-link active" : "nav-link"} onClick={() => setView("categories")}>Catégories</button>
            <button className={view === "users" ? "nav-link active" : "nav-link"} onClick={() => setView("users")}>Utilisateurs</button>
          </>}
        </nav>
        <div className="account-area">
          {user ? (
            <>
              <span className="user-label">{user.prenom} <small>{isClient ? "Mon compte" : user.role}</small></span>
              <button className="button button--quiet" onClick={() => void signOut()}>Déconnexion</button>
            </>
          ) : <a className="button button--dark" href="#connexion">Connexion</a>}
          {isCustomerInterface && user && <button className="cart-shortcut" onClick={() => setView("menu")} aria-label={`Panier, ${cart.length} articles`}>
            Panier <span>{cart.reduce((sum, line) => sum + line.quantity, 0)}</span>
          </button>}
        </div>
      </header>

      <div className="content-wrap">
        {notice && <div className="notice" role="status">{notice}<button onClick={() => setNotice(null)} aria-label="Fermer">×</button></div>}
        {pageError && <div className="alert" role="alert">{pageError}<button onClick={() => setPageError(null)} aria-label="Fermer">×</button></div>}

        {isClient && view === "clientHome" && <section className="dashboard-section">
          <div className="section-heading">
            <div><p className="eyebrow">Votre espace client</p><h2>Bonjour, {user.prenom}</h2></div>
            <button className="button button--accent" onClick={() => setView("menu")}>Commander</button>
          </div>
          <div className="stat-grid stat-grid--compact">
            <article className="stat-card"><span>Mes commandes</span><strong>{orders.length}</strong><small>Commandes enregistrées</small></article>
            <article className="stat-card"><span>Panier</span><strong>{cart.reduce((sum, line) => sum + line.quantity, 0)}</strong><small>{cart.length ? money(cartTotal) : "Aucun article sélectionné"}</small></article>
          </div>
          <section className="workspace-card">
            <div className="workspace-card__heading">
              <div><p className="eyebrow">Suivi</p><h3>Commandes récentes</h3></div>
              <button className="button button--quiet" onClick={() => setView("orders")}>Toutes mes commandes</button>
            </div>
            {!orders.length ? <p className="empty-state">Aucune commande pour le moment. Consultez le menu pour découvrir nos plats et boissons.</p> : (
              <div className="table-card">
                <div className="data-table data-table--orders"><strong>N°</strong><strong>Date</strong><strong>Service</strong><strong>Statut</strong><strong>Total</strong>
                  {orders.slice(0, 5).map((order) => (
                    <div className="table-row" key={order.id}>
                      <span>#{order.id}</span>
                      <span>{new Date(order.date).toLocaleDateString("fr-HT")}</span>
                      <span>{order.modeService}</span>
                      <span><i className="status-dot" />{order.statut}</span>
                      <strong>{money(order.montantTotal)}</strong>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
          <p className="fine-print">Les paiements en ligne ne sont pas encore disponibles. Une commande reste en attente jusqu’à confirmation du paiement.</p>
        </section>}

        {isCustomerInterface && view === "menu" && <>
          <section className="hero">
            <div className="hero-copy">
              <p className="eyebrow">Restaurant MALAWI · depuis toujours, avec cœur</p>
              <h1>Le goût de<br /><em>se retrouver.</em></h1>
              <p>Des recettes généreuses, des produits frais et des saveurs qui font du bien.</p>
              <a className="button button--accent" href="#menu">Découvrir le menu <span>↓</span></a>
            </div>
            <div className="hero-art" aria-hidden="true"><div className="plate"><span>Bon<br />appétit</span></div><i className="leaf leaf--one" /><i className="leaf leaf--two" /></div>
            <div className="hero-note"><span>01</span><p>Fait avec soin.<br />Servi avec le sourire.</p></div>
          </section>

          <section className="menu-section" id="menu">
            <div className="section-heading">
              <div><p className="eyebrow">À la carte</p><h2>Notre menu</h2></div>
              <p>Préparé chaque jour avec des ingrédients choisis.</p>
            </div>
            {loadingMenu && <p className="empty-state">Le menu arrive…</p>}
            {!loadingMenu && items.length === 0 && <p className="empty-state">Aucun produit n’est disponible pour le moment.</p>}
            {!loadingMenu && groupedItems.map(({ category, products }) => (
              <section className="menu-category" key={category}>
                <div className="category-heading"><h3>{category}</h3><span>{products.length} choix</span></div>
                <div className="menu-grid">
                  {products.map((product) => (
                    <article className="menu-card" key={product.id}>
                      <div className={`dish-illustration dish-illustration--${product.productType.toLowerCase()}`} aria-hidden="true">
                        <span>{product.productType === "Plat" ? "✳" : "◉"}</span>
                      </div>
                      <div className="menu-card__body">
                        <div className="menu-card__top"><span className="tag">{product.productType}</span><strong>{money(product.price)}</strong></div>
                        <h4>{product.name}</h4>
                        <p>{product.description || "Une recette savoureuse préparée avec attention."}</p>
                        <div className="menu-card__bottom">
                          <span className={product.isAvailable ? "stock stock--yes" : "stock stock--no"}>
                            {product.isAvailable ? "Disponible" : "Épuisé"}
                          </span>
                          <button className="add-button" disabled={!product.isAvailable} onClick={() => addToCart(product)}>
                            Ajouter <span>+</span>
                          </button>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </section>
            ))}
          </section>

          <section className="order-area">
            <div className="order-panel">
              <div className="section-heading section-heading--compact">
                <div><p className="eyebrow">Votre sélection</p><h2>Votre commande</h2></div>
                <span className="count-pill">{cart.reduce((sum, line) => sum + line.quantity, 0)} article(s)</span>
              </div>
              {cart.length === 0 ? <p className="muted">Ajoutez vos plats et boissons favoris pour commencer.</p> : <>
                <div className="cart-lines">
                  {cart.map(({ product, quantity }) => <div className="cart-line" key={product.id}>
                    <div><strong>{product.name}</strong><small>{money(product.price)} · {product.productType}</small></div>
                    <div className="quantity-control">
                      <button onClick={() => updateCartQuantity(product.id, quantity - 1)} aria-label={`Retirer ${product.name}`}>−</button>
                      <span>{quantity}</span>
                      <button onClick={() => updateCartQuantity(product.id, quantity + 1)} disabled={quantity >= product.quantity} aria-label={`Ajouter ${product.name}`}>+</button>
                    </div>
                  </div>)}
                </div>
                <div className="total-line"><span>Total estimé</span><strong>{money(cartTotal)}</strong></div>
                {!user ? <a className="button button--dark button--full" href="#connexion">Connectez-vous pour commander</a> : (
                  <form className="checkout-form" onSubmit={(event) => void submitOrder(event)}>
                    <label>Mode de service
                      <select value={serviceMode} onChange={(event) => setServiceMode(event.target.value as "Retrait" | "Livraison")}>
                        <option value="Retrait">Retrait au restaurant</option><option value="Livraison">Livraison</option>
                      </select>
                    </label>
                    {serviceMode === "Livraison" && <label>Adresse de livraison
                      <textarea required value={deliveryAddress} onChange={(event) => setDeliveryAddress(event.target.value)} maxLength={500} />
                    </label>}
                    <button className="button button--accent button--full" disabled={busy}>{busy ? "Envoi…" : "Confirmer la commande"}</button>
                    <p className="fine-print">Le paiement en ligne sera disponible après l’intégration du fournisseur.</p>
                  </form>
                )}
              </>}
            </div>
          </section>
        </>}

        {isClient && view === "orders" && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Espace client</p><h2>Mes commandes</h2></div><button className="button button--quiet" onClick={() => void loadOrders()}>Actualiser</button></div>
          {orders.length === 0 ? <p className="empty-state">Vous n’avez pas encore de commande.</p> : <div className="table-card">
            <div className="data-table data-table--orders"><strong>N°</strong><strong>Date</strong><strong>Service</strong><strong>Statut</strong><strong>Total</strong>
              {orders.map((order) => <div className="table-row" key={order.id}>
                <span>#{order.id}</span><span>{new Date(order.date).toLocaleDateString("fr-HT")}</span><span>{order.modeService}</span><span><i className="status-dot" />{order.statut}</span><strong>{money(order.montantTotal)}</strong>
              </div>)}
            </div>
          </div>}
          <p className="fine-print">Les commandes affichées restent en attente tant qu’un paiement n’a pas été confirmé par un fournisseur.</p>
        </section>}

        {isClient && view === "profile" && <section className="dashboard-section">
          <div className="section-heading">
            <div><p className="eyebrow">Espace client</p><h2>Mon profil</h2></div>
            <button className="button button--quiet" onClick={() => void loadClientProfile()}>Actualiser</button>
          </div>
          {clientProfile ? (
            <form className="editor-card" onSubmit={async (event) => {
              event.preventDefault();
              setBusy(true);
              setPageError(null);
              try {
                await api(`/api/clients/${clientProfile.id}`, {
                  method: "PUT",
                  body: JSON.stringify({
                    nom: clientProfile.nom,
                    prenom: clientProfile.prenom,
                    telephone: clientProfile.telephone || null,
                    adresse: clientProfile.adresse || null,
                    email: clientProfile.email || null,
                  }),
                });
                setNotice("Votre profil a été mis à jour.");
                await loadClientProfile();
              } catch (error) {
                setPageError(error instanceof Error ? error.message : "Votre profil n’a pas pu être mis à jour.");
              } finally {
                setBusy(false);
              }
            }}>
              <div className="form-grid">
                <label>Nom<input required maxLength={100} value={clientProfile.nom} onChange={(event) => setClientProfile({ ...clientProfile, nom: event.target.value })} /></label>
                <label>Prénom<input required maxLength={100} value={clientProfile.prenom} onChange={(event) => setClientProfile({ ...clientProfile, prenom: event.target.value })} /></label>
                <label>Adresse courriel<input type="email" maxLength={254} value={clientProfile.email ?? ""} readOnly /><small className="field-hint">L’adresse de connexion ne peut pas être modifiée ici.</small></label>
                <label>Téléphone<input type="tel" maxLength={30} value={clientProfile.telephone ?? ""} onChange={(event) => setClientProfile({ ...clientProfile, telephone: event.target.value })} /></label>
                <label className="wide-field">Adresse<input maxLength={500} value={clientProfile.adresse ?? ""} onChange={(event) => setClientProfile({ ...clientProfile, adresse: event.target.value })} /></label>
              </div>
              <div className="form-actions"><button className="button button--accent" disabled={busy}>{busy ? "Enregistrement…" : "Enregistrer mon profil"}</button></div>
            </form>
          ) : <p className="empty-state">Chargement du profil…</p>}
        </section>}

        {view === "products" && isAdmin && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Administration</p><h2>Gestion des produits</h2></div><button className="button button--quiet" onClick={() => void loadProductsForAdmin()}>Actualiser</button></div>
          <form className="editor-card" onSubmit={(event) => void saveProduct(event)}>
            <h3>{editingProductId ? "Modifier le produit" : "Ajouter un produit"}</h3>
            <div className="form-grid">
              <label>Nom<input required maxLength={150} value={productForm.nom} onChange={(event) => setProductForm({ ...productForm, nom: event.target.value })} /></label>
              <label>Type<select value={productForm.typeProduit} onChange={(event) => setProductForm({ ...productForm, typeProduit: event.target.value as Product["productType"] })}><option>Plat</option><option>Boisson</option></select></label>
              <label>Catégorie<select required value={productForm.categorie} onChange={(event) => setProductForm({ ...productForm, categorie: event.target.value })}><option value="">Choisir…</option>{activeCategories.map((category) => <option key={category.id}>{category.nom}</option>)}</select></label>
              <label>Prix (HTG)<input type="number" required min="0" step="0.01" value={productForm.prix} onChange={(event) => setProductForm({ ...productForm, prix: Number(event.target.value) })} /></label>
              <label>Quantité en stock<input type="number" required min="0" step="1" value={productForm.quantite} onChange={(event) => setProductForm({ ...productForm, quantite: Number(event.target.value) })} /></label>
              <label className="check-label"><input type="checkbox" checked={productForm.disponibilite} onChange={(event) => setProductForm({ ...productForm, disponibilite: event.target.checked })} /> Disponible</label>
              <label className="wide-field">Description<textarea maxLength={4000} value={productForm.description} onChange={(event) => setProductForm({ ...productForm, description: event.target.value })} /></label>
            </div>
            <div className="form-actions"><button className="button button--accent" disabled={busy || activeCategories.length === 0}>{editingProductId ? "Enregistrer" : "Créer le produit"}</button>{editingProductId && <button type="button" className="button button--quiet" onClick={resetProductForm}>Annuler</button>}</div>
          </form>
          <div className="admin-list">
            {items.map((product) => <article className="admin-product" key={product.id}>
              <div><span className="tag">{product.productType}</span><h3>{product.name}</h3><p>{product.category} · {money(product.price)} · stock {product.quantity}</p></div>
              <div className="admin-actions"><button className="button button--quiet" onClick={() => editProduct(product)}>Modifier</button><button className="button button--danger" onClick={() => void disableProduct(product.id)}>Désactiver</button></div>
            </article>)}
          </div>
        </section>}

        {view === "categories" && isAdmin && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Administration</p><h2>Catégories</h2></div><button className="button button--quiet" onClick={() => void loadCategories()}>Actualiser</button></div>
          <form className="inline-form editor-card" onSubmit={(event) => void createCategory(event)}>
            <label>Nouvelle catégorie<input required maxLength={100} value={categoryName} onChange={(event) => setCategoryName(event.target.value)} /></label>
            <button className="button button--accent">Ajouter</button>
          </form>
          <div className="admin-list">{categories.map((category) => <article className="admin-product" key={category.id}>
            <div><h3>{category.nom}</h3><p>{category.active ? "Active" : "Désactivée"}</p></div>
            <button className="button button--quiet" onClick={() => void toggleCategory(category)}>{category.active ? "Désactiver" : "Réactiver"}</button>
          </article>)}</div>
        </section>}

        {isStaff && ["dashboard", "manageOrders", "clients", "users", "deliveries", "payments", "reports", "history"].includes(view) && (
          <AdminWorkspace
            view={view === "manageOrders" ? "orders" : view as "dashboard" | "orders" | "clients" | "users" | "deliveries" | "payments" | "reports" | "history"}
            userId={user?.id ?? 0}
            role={user?.role ?? ""}
          />
        )}

        {user && !isClient && !isStaff && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Espace personnel</p><h2>Interface en préparation</h2></div></div>
          <p className="empty-state">Les outils correspondant à ce compte ne sont pas encore configurés.</p>
        </section>}

        {!user && <section className="auth-section" id="connexion">
          <div className="auth-intro"><p className="eyebrow">{staffOnly ? "Accès réservé au personnel" : "Bienvenue chez vous"}</p><h2>{staffOnly ? <>Espace<br /><em>personnel.</em></> : <>Un bon repas<br /><em>vous attend.</em></>}</h2><p>{staffOnly ? "Connectez-vous avec votre compte employé pour accéder aux outils du restaurant." : "Connectez-vous pour passer et suivre vos commandes."}</p></div>
          <div className="auth-card">
            {!staffOnly && <div className="auth-tabs"><button className={authMode === "login" ? "active" : ""} onClick={() => setAuthMode("login")}>Connexion</button><button className={authMode === "register" ? "active" : ""} onClick={() => setAuthMode("register")}>Créer un compte</button></div>}
            <form onSubmit={(event) => void submitAuth(event)}>
              {!staffOnly && authMode === "register" && <>
                <label>Nom<input name="nom" required maxLength={100} /></label>
                <label>Prénom<input name="prenom" required maxLength={100} /></label>
              </>}
              <label>Adresse courriel<input type="email" autoComplete="email" required maxLength={254} value={email} onChange={(event) => setEmail(event.target.value)} /></label>
              {!staffOnly && authMode === "register" && <>
                <label>Téléphone<input name="telephone" type="tel" maxLength={30} /></label>
                <label>Adresse<input name="adresse" maxLength={500} /></label>
              </>}
              <label>Mot de passe<input type="password" autoComplete={!staffOnly && authMode === "register" ? "new-password" : "current-password"} minLength={!staffOnly && authMode === "register" ? 8 : 1} maxLength={128} required value={password} onChange={(event) => setPassword(event.target.value)} /></label>
              {!staffOnly && authMode === "register" && <p className="fine-print">8 caractères minimum.</p>}
              <button className="button button--dark button--full" disabled={busy}>{busy ? "Veuillez patienter…" : !staffOnly && authMode === "register" ? "Créer mon compte" : "Se connecter"}</button>
            </form>
          </div>
        </section>}
      </div>

      <footer className="footer"><a className="brand brand--footer" href="/staff/" onClick={(event) => { event.preventDefault(); setView("dashboard"); }}><img className="brand-logo" src="/logo-malawi.jpg" alt="" /><strong>MALAWI</strong></a><span>Espace de gestion du restaurant.</span><span>© Malawi Resto</span></footer>
    </main>
  );
}
