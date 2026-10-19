# Base de données MySQL

Ces scripts ciblent MySQL 8.0.16 ou ultérieur. Ils créent le schéma `bd_malawi_resto` et les tables du périmètre initial : clients, rôles, menu, commandes, paiements MonCash/NatCash, factures, livraisons, remboursements et journal d'opérations.

## Créer la base

Dans PowerShell, depuis le dossier du projet :

```powershell
Get-Content -Raw .\Database\001_create_schema.sql | mysql -u root -p
Get-Content -Raw .\Database\002_seed_roles.sql | mysql -u root -p
```

Saisissez le mot de passe MySQL lorsque le client le demande. Le script `001` crée le schéma actuel, avec une table `Produit` et un champ `Type_Produit` (`Plat` ou `Boisson`). Le script `001` ne met pas à niveau une ancienne table existante.

Pour une base existante créée avec l'ancienne version de `001_create_schema.sql` et qui possède encore les tables `Plat` et `Boisson`, faites d'abord une sauvegarde complète, puis appliquez une seule fois `Database/003_merge_products.sql`. Cette migration conserve les produits et leurs lignes de commande, fusionne les deux tables dans `Produit`, puis supprime les anciennes tables. Ne l'exécutez pas sur une base neuve et ne lancez pas le nouveau script `001` sur l'ancienne base avant la migration. Si le schéma a été modifié manuellement, adaptez et validez la migration sur une copie de la base avant de l'utiliser.

Contrôlez ensuite la création :

```powershell
mysql -u root -p -D bd_malawi_resto -e "SHOW TABLES;"
```

## Fonctionnalités actuellement présentes

- `GET /api/health` vérifie que l'API est démarrée.
- `GET /api/menu` retourne les produits des catégories actives, avec leur catégorie, leur type, leur prix et leur disponibilité.
- `GET /api/products` et les autres routes `/api/products` gèrent dans la même ressource les plats et les boissons. Les commandes référencent les produits par `Id_Produit`.
- Le frontend React fournit deux interfaces indépendantes sur la même API : `/client/` pour les visiteurs et clients (menu, inscription, panier, commandes, factures et profil) ; `/staff/` pour la connexion et les outils du personnel. En développement avec Vite, les adresses sont `http://localhost:5173/client/` et `http://localhost:5173/staff/`. En production, elles sont servies par ASP.NET Core sur le même domaine.
- Les contrôleurs d'API couvrent également l'authentification, la gestion des utilisateurs, rôles, clients, catégories, produits, commandes, paiements, factures, livraisons, rapports, remboursements et historique. Les permissions, routes et règles métier sont détaillées dans `Controllers/README.md`.

La configuration MySQL n'est pas disponible sur toutes les machines : l'exécution réelle et les tests de bout en bout exigent un serveur MySQL avec les scripts ci-dessous appliqués. L'intégration des fournisseurs MonCash/NatCash n'est pas configurée ; la création de paiement reste donc désactivée (HTTP 501), aucune commande ne peut être déclarée payée par ces routes et aucune facture automatique n'est émise après paiement.

## Connexion de l'application

L'application lit la chaîne de connexion nommée `DBconnect` dans `ConnectionStrings`. Conservez le mot de passe hors des fichiers versionnés. En développement, vous pouvez le fournir avec les secrets utilisateur .NET :

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DBconnect" "server=localhost;database=bd_malawi_resto;user=root;password=VOTRE_MOT_DE_PASSE"
```

Adaptez l'utilisateur et les paramètres à votre installation MySQL. N'utilisez pas le compte `root` en production.

## Règles à respecter dans l'application

- Les nouvelles routes d'authentification hachent les mots de passe avec le `PasswordHasher` ASP.NET Core. L'ancien CRUD de la classe `Utilisateur` n'est pas utilisé par ces routes et ne doit pas servir à créer des comptes.
- La commande doit contenir au moins une ligne. Vérifiez la disponibilité et calculez le montant à partir des prix enregistrés, dans une transaction SQL.
- Un paiement n'est considéré réussi qu'après confirmation vérifiée de MonCash ou NatCash. Une référence de transaction ne constitue pas, à elle seule, une confirmation.
- La livraison ne concerne que les commandes en mode `Livraison`. Ses seuls statuts sont `En cours` et `Livrée` ; elle ne peut pas être annulée.
- Les remboursements après annulation exceptionnelle par l'administrateur sont exécutés manuellement chez le fournisseur puis enregistrés dans `Remboursement`.
- Les contraintes `CHECK` sont appliquées à partir de MySQL 8.0.16. La devise est initialisée à `HTG`; confirmez la devise réellement utilisée par le restaurant et les comptes marchands avant la mise en service.

## Limites d'intégration actuelles

Les modèles `Plat` et `Boisson` héritent maintenant du modèle commun `Produit` et leurs méthodes de persistance utilisent la table `Produit`. La création des commandes, le hachage des mots de passe et l'affectation des livraisons sont implémentés dans les API, mais doivent encore être testés avec une base MySQL réelle. La confirmation des paiements MonCash/NatCash et l'émission automatique des factures restent bloquées tant que l'intégration fournisseur n'est pas configurée.
