USE bd_malawi_resto;

CREATE TABLE Produit (
    Id_Produit INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Nom VARCHAR(150) NOT NULL,
    Quantite INT UNSIGNED NOT NULL DEFAULT 0,
    `Description` TEXT NULL,
    Prix DECIMAL(12, 2) NOT NULL,
    Categorie VARCHAR(100) NOT NULL,
    Type_Produit VARCHAR(20) NOT NULL,
    Disponibilite BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (Id_Produit),
    KEY IX_Produit_Categorie (Categorie),
    KEY IX_Produit_Disponibilite (Disponibilite),
    KEY IX_Produit_Type (Type_Produit),
    CONSTRAINT FK_Produit_Categorie
        FOREIGN KEY (Categorie) REFERENCES Categorie (Nom_Categorie)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Produit_Prix CHECK (Prix >= 0),
    CONSTRAINT CK_Produit_Type CHECK (Type_Produit IN ('Plat', 'Boisson'))
) ENGINE = InnoDB;

SET @boisson_id_offset := (SELECT COALESCE(MAX(IdPlat), 0) FROM Plat);

INSERT INTO Produit
    (Id_Produit, Nom, Quantite, `Description`, Prix, Categorie, Type_Produit, Disponibilite)
SELECT IdPlat, NomPlat, Quantite, `Description`, Prix, Categorie, 'Plat', Disponibilite
FROM Plat;

INSERT INTO Produit
    (Id_Produit, Nom, Quantite, `Description`, Prix, Categorie, Type_Produit, Disponibilite)
SELECT Id_Boisson + @boisson_id_offset, Nom_Boisson, Quantite, `Description`, Prix,
       Type_Boisson, 'Boisson', Disponibilite
FROM Boisson;

ALTER TABLE Detail_Commande
    ADD COLUMN Id_Produit INT UNSIGNED NULL AFTER Id_Commande;

UPDATE Detail_Commande
SET Id_Produit = IdPlat
WHERE IdPlat IS NOT NULL;

UPDATE Detail_Commande
SET Id_Produit = Id_Boisson + @boisson_id_offset
WHERE Id_Boisson IS NOT NULL;

ALTER TABLE Detail_Commande
    DROP FOREIGN KEY FK_Detail_Commande_Plat,
    DROP FOREIGN KEY FK_Detail_Commande_Boisson,
    DROP INDEX IX_Detail_Plat,
    DROP INDEX IX_Detail_Boisson,
    DROP CHECK CK_Detail_Un_Produit,
    MODIFY COLUMN Id_Produit INT UNSIGNED NOT NULL,
    ADD KEY IX_Detail_Produit (Id_Produit),
    ADD CONSTRAINT FK_Detail_Commande_Produit
        FOREIGN KEY (Id_Produit) REFERENCES Produit (Id_Produit)
        ON UPDATE CASCADE
        ON DELETE RESTRICT;

ALTER TABLE Detail_Commande
    DROP COLUMN IdPlat,
    DROP COLUMN Id_Boisson;

DROP TABLE Plat;
DROP TABLE Boisson;
