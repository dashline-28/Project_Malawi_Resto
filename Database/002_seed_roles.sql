USE bd_malawi_resto;

INSERT INTO Role (Nom_Role)
VALUES ('Client'), ('Caissier'), ('Administrateur'), ('Livreur'), ('Manager'), ('Superviseur'), ('Directeur')
ON DUPLICATE KEY UPDATE Nom_Role = VALUES(Nom_Role);

INSERT INTO Categorie (Nom_Categorie)
VALUES ('Entrées'), ('Plats principaux'), ('Boissons'), ('Desserts')
ON DUPLICATE KEY UPDATE Nom_Categorie = VALUES(Nom_Categorie);
