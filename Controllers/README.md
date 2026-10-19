# Contrôleurs HTTP

Les routes sont préfixées par `/api`. Les requêtes et réponses JSON utilisent les noms `camelCase`. Une session est ouverte par cookie HttpOnly après authentification ; les routes privées retournent `401` si la session est absente et `403` si le rôle ne convient pas.

## Contrôleurs

| Classe | Routes principales | Accès |
|---|---|---|
| `HomeController` | Pages MVC historiques : `/`, `/Home/Privacy`, `/Home/Error` | Public |
| `HealthController` | `GET /api/health` | Public |
| `AuthController` | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`, `POST /api/auth/change-password`, `POST /api/auth/bootstrap-admin` | Inscription client et connexion publiques ; profil, déconnexion et changement de mot de passe authentifiés ; initialisation administrateur protégée par une clé secrète à usage unique |
| `UsersController` | `GET/POST /api/users`, `GET/PUT/DELETE /api/users/{id}`, `PUT /api/users/{id}/password` | Administrateur |
| `RolesController` | `GET /api/roles` | Administrateur ; retourne les rôles attribuables aux employés, pas `Client` |
| `ClientsController` | `GET/POST /api/clients`, `GET/PUT /api/clients/{id}` | Client pour ses propres données ; administrateur/caissier pour la consultation générale et la création |
| `CategoriesController` | `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}` | Lecture publique ; modification administrateur |
| `MenuController` | `GET /api/menu` | Public |
| `ProductsController` | `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`, `PUT/DELETE /api/products/{id}` | Lecture publique ; écriture administrateur |
| `OrdersController` | `GET/POST /api/orders`, `GET /api/orders/{id}`, `PATCH /api/orders/{id}/status` | Session requise ; le client accède à ses commandes, le superviseur consulte toutes les commandes, le caissier/administrateur peut les gérer |
| `PaymentsController` | `GET /api/payments`, `GET /api/payments/{id}`, `POST /api/payments` | Session requise ; consultation limitée aux paiements du client ou au personnel autorisé (administrateur, caissier, superviseur) |
| `InvoicesController` | `GET /api/invoices`, `GET /api/invoices/{id}` | Client propriétaire, administrateur ou caissier |
| `DeliveriesController` | `GET /api/deliveries`, `GET /api/deliveries/couriers`, `POST /api/deliveries`, `PATCH /api/deliveries/{id}/status` | Administrateur/superviseur pour consulter les livreurs disponibles et affecter ; livreur pour ses livraisons |
| `ReportsController` | `GET /api/reports/sales?from=YYYY-MM-DD&to=YYYY-MM-DD` | Administrateur/caissier/superviseur/directeur |
| `RefundsController` | `GET/POST /api/refunds` | Administrateur |
| `HistoryController` | `GET /api/history?limit=100` | Administrateur/directeur (historique global) et superviseur (commandes, livraisons, paiements, remboursements) |

Les produits (plats et boissons) utilisent une seule ressource API et une seule table. Le champ `typeProduit` distingue les types `Plat` et `Boisson`.

## Règles et limites

- Les rôles proviennent de `Role`. Le script d'initialisation fournit `Client`, `Caissier`, `Administrateur`, `Livreur`, `Manager`, `Superviseur` et `Directeur`.
- Le directeur peut consulter les rapports et statistiques globaux ainsi que l'historique général. Il n'a pas accès à la gestion des commandes, des livraisons, des utilisateurs, des produits ou des paiements.
- Le superviseur peut consulter et suivre les commandes, les paiements et rapports de ventes, affecter les commandes prêtes à des livreurs actifs sans livraison en cours et consulter l'historique des commandes/livraisons/paiements/remboursements. Il ne peut ni créer/modifier les commandes, ni gérer les utilisateurs, produits ou catégories. La caisse n'ayant pas de module distinct dans le schéma actuel, sa supervision se limite aux opérations de paiement enregistrées.
- Dans l'espace superviseur, les commandes et livraisons se rafraîchissent automatiquement toutes les 10 secondes. La caisse n'est pas modifiable depuis cet espace ; les paiements en ligne ne sont pas disponibles tant que MonCash/NatCash n'est pas intégré.
- Les interfaces sont deux points d'entrée web séparés : `/client/` pour les visiteurs et clients (menu, inscription, panier, commandes, factures et profil), et `/staff/` pour le personnel. Elles partagent l'API et la base de données ; une connexion client depuis l'espace personnel et une connexion employé depuis l'espace client redirigent vers le bon espace. Le rôle `Client` reste utilisé par l'inscription et l'autorisation, mais n'est pas proposé à la création ou à la modification d'un compte employé.
- Les mots de passe sont hachés avec le `PasswordHasher` ASP.NET Core. L'inscription publique ne crée que des comptes `Client`.
- Pour créer le premier administrateur, définir le secret `Bootstrap:AdminSetupKey` (au moins 32 caractères) dans les secrets de développement ou le gestionnaire de secrets de l'hébergeur. Appeler `POST /api/auth/bootstrap-admin` une seule fois avec le même secret dans `cleConfiguration`, puis retirer le secret de démarrage. Cette route refuse toute création si un administrateur existe déjà.
- Les commandes sont créées dans une transaction et leur total est calculé depuis les prix de la base. Les lignes vides, produits indisponibles, demandes supérieures au stock indiqué et combinaisons mode/adresse incohérentes sont refusés. L'état `Confirmée` n'est pas attribuable manuellement avant preuve du paiement.
- Les transitions de livraison et de commande sont restreintes aux statuts du schéma SQL. Une commande de livraison ne devient `En livraison` qu'après son affectation.
- Le schéma ne permet que `MonCash` et `NatCash`. L'API ne déclare pas de paiement réussi et retourne `501 Not Implemented` pour sa création tant que l'intégration fournisseur et la vérification serveur des transactions ne sont pas configurées. En conséquence, la génération automatique de facture après paiement n'est pas encore activée.
- L'accès aux rapports dépend des paiements marqués `Réussi` dans la base ; ces données doivent provenir d'une confirmation fiable et ne doivent pas être modifiées manuellement pour simuler une transaction.
- `HistoryController` consulte `Historique_Operation`. Les créations de commandes, changements d'état des commandes et opérations d'affectation/livraison/remboursement y sont journalisés dans la même transaction que l'opération. Les autres mutations ne sont pas encore toutes journalisées.
- Les routes retournent `503 Service Unavailable` si MySQL est inaccessible. Les détails internes des exceptions restent dans les journaux serveur et ne sont pas renvoyés au client.

## Configuration requise

La chaîne `ConnectionStrings:DBconnect` doit être fournie par la configuration .NET. Exécuter les scripts de `Database` avant d'utiliser les routes dépendantes de la base. Le fournisseur MySQL doit être joignable depuis l'application.
