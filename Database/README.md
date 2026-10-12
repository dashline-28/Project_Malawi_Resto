# Base de données MySQL

Ces scripts ciblent MySQL 8.0.16 ou ultérieur. Ils créent le schéma `bd_malawi_resto` et les tables du périmètre initial : clients, rôles, menu, commandes, paiements MonCash/NatCash, factures, livraisons, remboursements et journal d'opérations.

## Créer la base

Dans PowerShell, depuis le dossier du projet :

```powershell
Get-Content -Raw .\Database\001_create_schema.sql | mysql -u root -p
Get-Content -Raw .\Database\002_seed_roles.sql | mysql -u root -p
```

Saisissez le mot de passe MySQL lorsque le client le demande. Le premier script utilise `CREATE DATABASE/TABLE IF NOT EXISTS` et ne supprime aucune table. Il ne met toutefois pas à niveau une table existante dont la structure diffère. Pour une base de développement existante, sauvegardez-la avant toute modification et vérifiez son schéma séparément.

Contrôlez ensuite la création :

```powershell
mysql -u root -p -D bd_malawi_resto -e "SHOW TABLES;"
```

## Connexion de l'application

L'application lit la chaîne de connexion nommée `DBconnect` dans `ConnectionStrings`. Conservez le mot de passe hors des fichiers versionnés. En développement, vous pouvez le fournir avec les secrets utilisateur .NET :

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DBconnect" "server=localhost;database=bd_malawi_resto;user=root;password=VOTRE_MOT_DE_PASSE"
```

Adaptez l'utilisateur et les paramètres à votre installation MySQL. N'utilisez pas le compte `root` en production.

## Règles à respecter dans l'application

- Les valeurs de `Utilisateur.mot_de_passe` doivent être des hashes produits par un algorithme de hachage de mots de passe, jamais des mots de passe en clair.
- N'insérez pas de comptes par l'ancien CRUD `Utilisateur` avant d'y ajouter le hachage : il enregistre actuellement la valeur reçue telle quelle et certaines de ses requêtes utilisent des noms de colonnes différents.
- La commande doit contenir au moins une ligne. Vérifiez la disponibilité et calculez le montant à partir des prix enregistrés, dans une transaction SQL.
- Un paiement n'est considéré réussi qu'après confirmation vérifiée de MonCash ou NatCash. Une référence de transaction ne constitue pas, à elle seule, une confirmation.
- La livraison ne concerne que les commandes en mode `Livraison`. Ses seuls statuts sont `En cours` et `Livrée` ; elle ne peut pas être annulée.
- Les remboursements après annulation exceptionnelle par l'administrateur sont exécutés manuellement chez le fournisseur puis enregistrés dans `Remboursement`.
- Les contraintes `CHECK` sont appliquées à partir de MySQL 8.0.16. La devise est initialisée à `HTG`; confirmez la devise réellement utilisée par le restaurant et les comptes marchands avant la mise en service.

## Limites d'intégration actuelles

Le dépôt contient des modèles C# antérieurs à ce schéma. Certains noms de colonnes et statuts qu'ils utilisent ne correspondent pas aux règles métier retenues. En particulier, les modèles de commande, de paiement et d'utilisateur doivent être adaptés avant d'appeler ces tables depuis les nouvelles API. La création de commande, la confirmation automatique de paiement, l'affectation des livraisons et le hachage des mots de passe ne sont pas encore intégrés. Ces scripts ne modifient pas les données existantes et ne doivent pas être considérés comme une migration d'une base déjà utilisée.
