import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";

type Product = {
  id: number;
  name: string;
  description: string | null;
  price: number;
  category: string;
  isAvailable: boolean;
  productType: "Plat" | "Boisson";
  quantity: number;
};
type User = { id: number; nom: string; prenom: string; email: string; role: string };
type Order = {
  id: number;
  date: string;
  montantTotal: number;
  devise: string;
  statut: string;
  modeService: string;
};
type ClientProfile = {
  id: number;
  nom: string;
  prenom: string;
  telephone: string | null;
  adresse: string | null;
  email: string | null;
};
type Invoice = {
  id: number;
  numero: string;
  dateEmission: string;
  montantTotal: number;
  devise: string;
  idCommande: number;
};
type CartLine = { product: Product; quantity: number };
type ClientView = "home" | "menu" | "cart" | "orders" | "invoices" | "profile";

async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  const response = await fetch(path, { ...init, headers, credentials: "same-origin" });
  if (!response.ok) {
    let message = `La requête a échoué (${response.status}).`;
    try {
      const problem = (await response.json()) as { title?: string; detail?: string };
      message = problem.title ?? problem.detail ?? message;
    } catch {
      // Some error responses do not include JSON problem details.
    }
    throw new Error(message);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

const money = (amount: number, currency = "HTG") =>
  new Intl.NumberFormat("fr-HT", { style: "currency", currency }).format(amount);

export default function ClientApp() {
  const [user, setUser] = useState<User | null>(null);
  const [view, setView] = useState<ClientView>("home");
  const [items, setItems] = useState<Product[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [profile, setProfile] = useState<ClientProfile | null>(null);
  const [authMode, setAuthMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [serviceMode, setServiceMode] = useState<"Retrait" | "Livraison">("Retrait");
  const [deliveryAddress, setDeliveryAddress] = useState("");
  const [busy, setBusy] = useState(false);
  const [loadingMenu, setLoadingMenu] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const cartTotal = useMemo(
    () => cart.reduce((total, line) => total + line.product.price * line.quantity, 0),
    [cart],
  );
  const cartCount = cart.reduce((count, line) => count + line.quantity, 0);
  const groupedItems = useMemo(
    () => [...new Set(items.map((item) => item.category))].map((category) => ({
      category,
      products: items.filter((item) => item.category === category),
    })),
    [items],
  );

  const loadMenu = useCallback(async () => {
    setLoadingMenu(true);
    try {
      setItems(await api<Product[]>("/api/menu"));
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Impossible de charger le menu.");
    } finally {
      setLoadingMenu(false);
    }
  }, []);

  const loadOrders = useCallback(async () => {
    setOrders(await api<Order[]>("/api/orders"));
  }, []);

  const loadInvoices = useCallback(async () => {
    setInvoices(await api<Invoice[]>("/api/invoices"));
  }, []);

  const loadProfile = useCallback(async () => {
    const profiles = await api<ClientProfile[]>("/api/clients");
    setProfile(profiles[0] ?? null);
  }, []);

  useEffect(() => {
    void loadMenu();
    api<User>("/api/auth/me")
      .then((currentUser) => {
        if (currentUser.role === "Client") setUser(currentUser);
        else window.location.replace("/staff/");
      })
      .catch((sessionError: unknown) => {
        if (sessionError instanceof Error && !sessionError.message.includes("(401)")) {
          console.error("Impossible de vérifier la session client.", sessionError);
        }
      });
  }, [loadMenu]);

  useEffect(() => {
    if (!user) return;
    const loads: Promise<void>[] = [];
    if (view === "home" || view === "orders") loads.push(loadOrders());
    if (view === "invoices") loads.push(loadInvoices());
    if (view === "profile") loads.push(loadProfile());
    void Promise.all(loads).catch((loadError: unknown) =>
      setError(loadError instanceof Error ? loadError.message : "Impossible de charger votre espace client."),
    );
  }, [view, user, loadOrders, loadInvoices, loadProfile]);

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (authMode === "login") {
        const currentUser = await api<User>("/api/auth/login", {
          method: "POST",
          body: JSON.stringify({ email, motDePasse: password }),
        });
        if (currentUser.role !== "Client") {
          window.location.assign("/staff/");
          return;
        }
        setUser(currentUser);
        setView("home");
        setPassword("");
        setNotice(`Bienvenue, ${currentUser.prenom}.`);
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
        setPassword("");
        setNotice("Compte créé. Vous pouvez maintenant vous connecter.");
      }
    } catch (authError) {
      setError(authError instanceof Error ? authError.message : "L’authentification a échoué.");
    } finally {
      setBusy(false);
    }
  }

  async function signOut() {
    try {
      await api("/api/auth/logout", { method: "POST" });
      setUser(null);
      setCart([]);
      setView("home");
      setNotice("Vous êtes déconnecté.");
    } catch (signOutError) {
      setError(signOutError instanceof Error ? signOutError.message : "La déconnexion a échoué.");
    }
  }

  function addToCart(product: Product) {
    setCart((current) => {
      const line = current.find((entry) => entry.product.id === product.id);
      return line
        ? current.map((entry) => entry.product.id === product.id
          ? { ...entry, quantity: Math.min(entry.quantity + 1, product.quantity) }
          : entry)
        : [...current, { product, quantity: 1 }];
    });
    setNotice(`${product.name} ajouté au panier.`);
    setError(null);
  }

  function updateQuantity(productId: number, quantity: number) {
    setCart((current) => quantity <= 0
      ? current.filter((line) => line.product.id !== productId)
      : current.map((line) => line.product.id === productId
        ? { ...line, quantity: Math.min(quantity, line.product.quantity) }
        : line));
  }

  async function placeOrder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
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
      setNotice("Votre commande est enregistrée. Le paiement en ligne n’est pas encore disponible.");
      setView("orders");
      await loadOrders();
    } catch (orderError) {
      setError(orderError instanceof Error ? orderError.message : "La commande n’a pas pu être enregistrée.");
    } finally {
      setBusy(false);
    }
  }

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!profile) return;
    setBusy(true);
    setError(null);
    try {
      await api(`/api/clients/${profile.id}`, {
        method: "PUT",
        body: JSON.stringify({
          nom: profile.nom,
          prenom: profile.prenom,
          telephone: profile.telephone || null,
          adresse: profile.adresse || null,
          email: profile.email || null,
        }),
      });
      setNotice("Votre profil a été mis à jour.");
      await loadProfile();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "Votre profil n’a pas pu être mis à jour.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="app-shell client-portal">
      <header className="topbar topbar--client">
        <a className="brand" href="/client/" onClick={(event) => { event.preventDefault(); setView("home"); }}>
          <img className="brand-logo" src="/logo-malawi.jpg" alt="Logo Malawi Hôtel" />
          <span><strong>MALAWI</strong><small>Espace client</small></span>
        </a>
        <nav className="main-nav" aria-label="Navigation client">
          <button className={view === "home" ? "nav-link active" : "nav-link"} onClick={() => setView("home")}>Accueil</button>
          <button className={view === "menu" ? "nav-link active" : "nav-link"} onClick={() => setView("menu")}>Menu</button>
          {user && <>
            <button className={view === "orders" ? "nav-link active" : "nav-link"} onClick={() => setView("orders")}>Mes commandes</button>
            <button className={view === "invoices" ? "nav-link active" : "nav-link"} onClick={() => setView("invoices")}>Mes factures</button>
            <button className={view === "profile" ? "nav-link active" : "nav-link"} onClick={() => setView("profile")}>Mon profil</button>
          </>}
        </nav>
        <div className="account-area">
          {user ? <>
            <span className="user-label">{user.prenom}<small>Mon compte</small></span>
            <button className="button button--quiet" onClick={() => void signOut()}>Déconnexion</button>
            <button className="cart-shortcut" onClick={() => setView("cart")} aria-label={`Panier, ${cartCount} articles`}>
              Panier <span>{cartCount}</span>
            </button>
          </> : <button className="button button--dark" onClick={() => document.getElementById("connexion")?.scrollIntoView({ behavior: "smooth" })}>Connexion</button>}
        </div>
      </header>

      <div className="content-wrap">
        {notice && <div className="notice" role="status">{notice}<button onClick={() => setNotice(null)} aria-label="Fermer">×</button></div>}
        {error && <div className="alert" role="alert">{error}<button onClick={() => setError(null)} aria-label="Fermer">×</button></div>}

        {view === "home" && <section className="hero">
          <div className="hero-copy">
            <p className="eyebrow">Restaurant MALAWI · Espace client</p>
            <h1>{user ? <>Bonjour,<br /><em>{user.prenom}.</em></> : <>Le goût de<br /><em>se retrouver.</em></>}</h1>
            <p>Consultez notre menu, commandez vos plats préférés et suivez vos commandes en un seul endroit.</p>
            <button className="button button--accent" onClick={() => setView("menu")}>Découvrir le menu <span>→</span></button>
            {user && <button className="button button--quiet" onClick={() => setView("orders")}>Suivre mes commandes</button>}
          </div>
          <div className="hero-art" aria-hidden="true"><div className="plate"><span>Bon<br />appétit</span></div><i className="leaf leaf--one" /><i className="leaf leaf--two" /></div>
          <div className="hero-note"><span>01</span><p>Fait avec soin.<br />Servi avec le sourire.</p></div>
        </section>}

        {view === "menu" && <section className="menu-section">
          <div className="section-heading"><div><p className="eyebrow">À la carte</p><h2>Notre menu</h2></div><p>Choisissez vos plats et boissons.</p></div>
          {loadingMenu && <p className="empty-state">Chargement du menu…</p>}
          {!loadingMenu && !items.length && <p className="empty-state">Aucun produit n’est disponible pour le moment.</p>}
          {groupedItems.map(({ category, products }) => <section className="menu-category" key={category}>
            <div className="category-heading"><h3>{category}</h3><span>{products.length} choix</span></div>
            <div className="menu-grid">{products.map((product) => <article className="menu-card" key={product.id}>
              <div className={`dish-illustration dish-illustration--${product.productType.toLowerCase()}`} aria-hidden="true"><span>{product.productType === "Plat" ? "✳" : "◉"}</span></div>
              <div className="menu-card__body">
                <div className="menu-card__top"><span className="tag">{product.productType}</span><strong>{money(product.price)}</strong></div>
                <h4>{product.name}</h4><p>{product.description || "Préparé avec attention."}</p>
                <div className="menu-card__bottom">
                  <span className={product.isAvailable ? "stock stock--yes" : "stock stock--no"}>{product.isAvailable ? "Disponible" : "Épuisé"}</span>
                  <button className="add-button" disabled={!user || !product.isAvailable} onClick={() => addToCart(product)}>
                    {!user ? "Connectez-vous" : <>Ajouter <span>+</span></>}
                  </button>
                </div>
              </div>
            </article>)}</div>
          </section>)}
          {!user && <p className="fine-print">Connectez-vous ou créez un compte pour ajouter des produits au panier et commander.</p>}
        </section>}

        {view === "cart" && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Votre sélection</p><h2>Mon panier</h2></div><button className="button button--quiet" onClick={() => setView("menu")}>Continuer mes achats</button></div>
          {!cart.length ? <p className="empty-state">Votre panier est vide. Consultez le menu pour commencer.</p> : <>
            <div className="workspace-card"><div className="cart-lines">{cart.map(({ product, quantity }) => <div className="cart-line" key={product.id}>
              <div><strong>{product.name}</strong><small>{money(product.price)} · {product.productType}</small></div>
              <div className="quantity-control">
                <button onClick={() => updateQuantity(product.id, quantity - 1)} aria-label={`Retirer ${product.name}`}>−</button>
                <span>{quantity}</span>
                <button onClick={() => updateQuantity(product.id, quantity + 1)} disabled={quantity >= product.quantity} aria-label={`Ajouter ${product.name}`}>+</button>
              </div><strong>{money(product.price * quantity)}</strong>
            </div>)}</div><div className="total-line"><span>Total estimé</span><strong>{money(cartTotal)}</strong></div></div>
            <form className="checkout-form editor-card" onSubmit={(event) => void placeOrder(event)}>
              <h3>Livraison ou retrait</h3>
              <label>Mode de service<select value={serviceMode} onChange={(event) => setServiceMode(event.target.value as "Retrait" | "Livraison")}><option value="Retrait">À emporter / retrait</option><option value="Livraison">Livraison</option></select></label>
              {serviceMode === "Livraison" && <label>Adresse de livraison<textarea required maxLength={500} value={deliveryAddress} onChange={(event) => setDeliveryAddress(event.target.value)} /></label>}
              <button className="button button--accent" disabled={busy}>{busy ? "Envoi…" : "Valider ma commande"}</button>
              <p className="fine-print">Le montant est calculé par le serveur. Le paiement en ligne sera disponible après l’intégration du fournisseur.</p>
            </form>
          </>}
        </section>}

        {view === "orders" && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Suivi client</p><h2>Mes commandes</h2></div><button className="button button--quiet" onClick={() => void loadOrders()}>Actualiser</button></div>
          {!orders.length ? <p className="empty-state">Vous n’avez pas encore de commande.</p> : <div className="table-card">
            <div className="data-table data-table--orders"><strong>N°</strong><strong>Date</strong><strong>Service</strong><strong>Statut</strong><strong>Total</strong>
              {orders.map((order) => <div className="table-row" key={order.id}>
                <span>#{order.id}</span><span>{new Date(order.date).toLocaleDateString("fr-HT")}</span><span>{order.modeService}</span>
                <span><i className="status-dot" />{order.statut}</span><strong>{money(order.montantTotal, order.devise)}</strong>
              </div>)}
            </div>
          </div>}
        </section>}

        {view === "invoices" && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Documents</p><h2>Mes factures</h2></div><button className="button button--quiet" onClick={() => void loadInvoices()}>Actualiser</button></div>
          {!invoices.length ? <p className="empty-state">Aucune facture n’est disponible. Les factures seront émises après confirmation du paiement.</p> : <div className="table-card">
            <div className="data-table data-table--orders"><strong>Facture</strong><strong>Date</strong><strong>Commande</strong><strong>Montant</strong><strong>Devise</strong>
              {invoices.map((invoice) => <div className="table-row" key={invoice.id}>
                <span>{invoice.numero}</span><span>{new Date(invoice.dateEmission).toLocaleDateString("fr-HT")}</span>
                <span>#{invoice.idCommande}</span><strong>{money(invoice.montantTotal, invoice.devise)}</strong><span>{invoice.devise}</span>
              </div>)}
            </div>
          </div>}
        </section>}

        {view === "profile" && <section className="dashboard-section">
          <div className="section-heading"><div><p className="eyebrow">Mon compte</p><h2>Mon profil</h2></div><button className="button button--quiet" onClick={() => void loadProfile()}>Actualiser</button></div>
          {!profile ? <p className="empty-state">Chargement du profil…</p> : <form className="editor-card" onSubmit={(event) => void saveProfile(event)}>
            <div className="form-grid">
              <label>Nom<input required maxLength={100} value={profile.nom} onChange={(event) => setProfile({ ...profile, nom: event.target.value })} /></label>
              <label>Prénom<input required maxLength={100} value={profile.prenom} onChange={(event) => setProfile({ ...profile, prenom: event.target.value })} /></label>
              <label>Adresse courriel<input type="email" value={profile.email ?? ""} readOnly /><small className="field-hint">L’adresse de connexion ne peut pas être modifiée ici.</small></label>
              <label>Téléphone<input type="tel" maxLength={30} value={profile.telephone ?? ""} onChange={(event) => setProfile({ ...profile, telephone: event.target.value })} /></label>
              <label className="wide-field">Adresse<input maxLength={500} value={profile.adresse ?? ""} onChange={(event) => setProfile({ ...profile, adresse: event.target.value })} /></label>
            </div><div className="form-actions"><button className="button button--accent" disabled={busy}>{busy ? "Enregistrement…" : "Enregistrer mon profil"}</button></div>
          </form>}
        </section>}

        {!user && <section className="auth-section" id="connexion">
          <div className="auth-intro"><p className="eyebrow">Votre compte Malawi</p><h2>{authMode === "login" ? <>Bienvenue<br /><em>chez vous.</em></> : <>Rejoignez<br /><em>notre table.</em></>}</h2><p>Un compte client vous permet de commander et de suivre vos achats.</p></div>
          <div className="auth-card">
            <div className="auth-tabs">
              <button type="button" className={authMode === "login" ? "active" : ""} onClick={() => setAuthMode("login")}>Connexion</button>
              <button type="button" className={authMode === "register" ? "active" : ""} onClick={() => setAuthMode("register")}>Créer un compte</button>
            </div>
            <form onSubmit={(event) => void submitAuth(event)}>
              {authMode === "register" && <>
                <label>Nom<input name="nom" required maxLength={100} /></label>
                <label>Prénom<input name="prenom" required maxLength={100} /></label>
              </>}
              <label>Adresse courriel<input type="email" autoComplete="email" required maxLength={254} value={email} onChange={(event) => setEmail(event.target.value)} /></label>
              {authMode === "register" && <>
                <label>Téléphone<input name="telephone" type="tel" maxLength={30} /></label>
                <label>Adresse<input name="adresse" maxLength={500} /></label>
              </>}
              <label>Mot de passe<input type="password" autoComplete={authMode === "login" ? "current-password" : "new-password"} minLength={authMode === "register" ? 8 : 1} maxLength={128} required value={password} onChange={(event) => setPassword(event.target.value)} /></label>
              {authMode === "register" && <p className="fine-print">8 caractères minimum.</p>}
              <button className="button button--dark button--full" disabled={busy}>{busy ? "Veuillez patienter…" : authMode === "login" ? "Se connecter" : "Créer mon compte"}</button>
            </form>
          </div>
        </section>}
      </div>

      <footer className="footer"><a className="brand brand--footer" href="/client/" onClick={(event) => { event.preventDefault(); setView("home"); }}><img className="brand-logo" src="/logo-malawi.jpg" alt="" /><strong>MALAWI</strong></a><span>Le plaisir de bien manger, tout simplement.</span><span>© Malawi Resto</span></footer>
    </main>
  );
}
