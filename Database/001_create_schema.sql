CREATE DATABASE IF NOT EXISTS bd_malawi_resto
    CHARACTER SET utf8mb4
    ;

USE bd_malawi_resto;

CREATE TABLE IF NOT EXISTS Role (
    Id_Role SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Nom_Role VARCHAR(30) NOT NULL,
    PRIMARY KEY (Id_Role),
    UNIQUE KEY UQ_Role_Nom (Nom_Role)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Utilisateur (
    idUtilisateur INT UNSIGNED NOT NULL AUTO_INCREMENT,
    nom VARCHAR(100) NOT NULL,
    prenom VARCHAR(100) NOT NULL,
    email VARCHAR(254) NOT NULL,
    mot_de_passe VARCHAR(255) NOT NULL COMMENT 'Hash de mot de passe uniquement, jamais le mot de passe en clair',
    role VARCHAR(30) NOT NULL,
    actif BOOLEAN NOT NULL DEFAULT TRUE,
    Date_Creation DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (idUtilisateur),
    UNIQUE KEY UQ_Utilisateur_Email (email),
    KEY IX_Utilisateur_Role (role),
    CONSTRAINT FK_Utilisateur_Role
        FOREIGN KEY (role) REFERENCES Role (Nom_Role)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Client (
    idClient INT UNSIGNED NOT NULL AUTO_INCREMENT,
    nomClient VARCHAR(100) NOT NULL,
    prenomClient VARCHAR(100) NOT NULL,
    telephone VARCHAR(30) NULL,
    adresse VARCHAR(500) NULL,
    email VARCHAR(254) NULL,
    Id_Utilisateur INT UNSIGNED NULL,
    Date_Creation DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (idClient),
    UNIQUE KEY UQ_Client_Email (email),
    UNIQUE KEY UQ_Client_Utilisateur (Id_Utilisateur),
    CONSTRAINT FK_Client_Utilisateur
        FOREIGN KEY (Id_Utilisateur) REFERENCES Utilisateur (idUtilisateur)
        ON UPDATE CASCADE
        ON DELETE SET NULL
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Categorie (
    Id_Categorie INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Nom_Categorie VARCHAR(100) NOT NULL,
    Active BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (Id_Categorie),
    UNIQUE KEY UQ_Categorie_Nom (Nom_Categorie)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Produit (
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

CREATE TABLE IF NOT EXISTS Commande (
    Id_Commande INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Date_Commande DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Montant_Total DECIMAL(12, 2) NOT NULL DEFAULT 0,
    Devise CHAR(3) NOT NULL DEFAULT 'HTG',
    Statut VARCHAR(40) NOT NULL DEFAULT 'En attente de paiement',
    Mode_Service VARCHAR(20) NOT NULL,
    Adresse_Livraison VARCHAR(500) NULL,
    Id_Client INT UNSIGNED NOT NULL,
    Id_Utilisateur INT UNSIGNED NULL COMMENT 'Employé ayant saisi la commande, si applicable',
    PRIMARY KEY (Id_Commande),
    KEY IX_Commande_Client_Date (Id_Client, Date_Commande),
    KEY IX_Commande_Statut_Date (Statut, Date_Commande),
    CONSTRAINT FK_Commande_Client
        FOREIGN KEY (Id_Client) REFERENCES Client (idClient)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Commande_Utilisateur
        FOREIGN KEY (Id_Utilisateur) REFERENCES Utilisateur (idUtilisateur)
        ON UPDATE CASCADE
        ON DELETE SET NULL,
    CONSTRAINT CK_Commande_Montant CHECK (Montant_Total >= 0),
    CONSTRAINT CK_Commande_Statut CHECK (
        Statut IN (
            'En attente de paiement',
            'Confirmée',
            'En préparation',
            'Prête',
            'En livraison',
            'Livrée',
            'Retirée au restaurant',
            'Annulée'
        )
    ),
    CONSTRAINT CK_Commande_Mode_Service CHECK (Mode_Service IN ('Livraison', 'Retrait')),
    CONSTRAINT CK_Commande_Adresse_Livraison CHECK (
        (Mode_Service = 'Livraison' AND Adresse_Livraison IS NOT NULL AND TRIM(Adresse_Livraison) <> '')
        OR
        (Mode_Service = 'Retrait' AND Adresse_Livraison IS NULL)
    )
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Detail_Commande (
    Id_Detail INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Id_Commande INT UNSIGNED NOT NULL,
    Id_Produit INT UNSIGNED NOT NULL,
    Quantite INT UNSIGNED NOT NULL,
    Prix_Unitaire DECIMAL(12, 2) NOT NULL,
    Montant_Ligne DECIMAL(12, 2)
        GENERATED ALWAYS AS (Quantite * Prix_Unitaire) STORED,
    PRIMARY KEY (Id_Detail),
    KEY IX_Detail_Commande (Id_Commande),
    KEY IX_Detail_Produit (Id_Produit),
    CONSTRAINT FK_Detail_Commande_Commande
        FOREIGN KEY (Id_Commande) REFERENCES Commande (Id_Commande)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Detail_Commande_Produit
        FOREIGN KEY (Id_Produit) REFERENCES Produit (Id_Produit)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Detail_Quantite CHECK (Quantite > 0),
    CONSTRAINT CK_Detail_Prix CHECK (Prix_Unitaire >= 0)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Paiement (
    Id_Paiement INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Date_Paiement DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Montant DECIMAL(12, 2) NOT NULL,
    Devise CHAR(3) NOT NULL DEFAULT 'HTG',
    Mode_Paiement VARCHAR(20) NOT NULL,
    Reference_Transaction VARCHAR(191) NULL,
    Statut VARCHAR(20) NOT NULL DEFAULT 'En attente',
    Id_Commande INT UNSIGNED NOT NULL,
    PRIMARY KEY (Id_Paiement),
    UNIQUE KEY UQ_Paiement_Reference (Reference_Transaction),
    KEY IX_Paiement_Commande_Date (Id_Commande, Date_Paiement),
    CONSTRAINT FK_Paiement_Commande
        FOREIGN KEY (Id_Commande) REFERENCES Commande (Id_Commande)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Paiement_Montant CHECK (Montant > 0),
    CONSTRAINT CK_Paiement_Mode CHECK (Mode_Paiement IN ('MonCash', 'NatCash')),
    CONSTRAINT CK_Paiement_Statut CHECK (
        Statut IN ('En attente', 'Réussi', 'Échoué', 'Remboursé')
    )
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Facture (
    Id_Facture INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Numero_Facture VARCHAR(40) NOT NULL,
    Date_Emission DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Montant_Total DECIMAL(12, 2) NOT NULL,
    Devise CHAR(3) NOT NULL DEFAULT 'HTG',
    Id_Commande INT UNSIGNED NOT NULL,
    PRIMARY KEY (Id_Facture),
    UNIQUE KEY UQ_Facture_Numero (Numero_Facture),
    UNIQUE KEY UQ_Facture_Commande (Id_Commande),
    CONSTRAINT FK_Facture_Commande
        FOREIGN KEY (Id_Commande) REFERENCES Commande (Id_Commande)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Facture_Montant CHECK (Montant_Total >= 0)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Livraison (
    Id_Livraison INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Id_Commande INT UNSIGNED NOT NULL,
    Id_Livreur INT UNSIGNED NOT NULL,
    Date_Debut DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Date_Livree DATETIME NULL,
    Statut VARCHAR(20) NOT NULL DEFAULT 'En cours',
    PRIMARY KEY (Id_Livraison),
    UNIQUE KEY UQ_Livraison_Commande (Id_Commande),
    KEY IX_Livraison_Livreur_Statut (Id_Livreur, Statut),
    CONSTRAINT FK_Livraison_Commande
        FOREIGN KEY (Id_Commande) REFERENCES Commande (Id_Commande)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Livraison_Livreur
        FOREIGN KEY (Id_Livreur) REFERENCES Utilisateur (idUtilisateur)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Livraison_Statut CHECK (Statut IN ('En cours', 'Livrée')),
    CONSTRAINT CK_Livraison_Date CHECK (
        (Statut = 'En cours' AND Date_Livree IS NULL)
        OR
        (Statut = 'Livrée' AND Date_Livree IS NOT NULL)
    )
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Remboursement (
    Id_Remboursement INT UNSIGNED NOT NULL AUTO_INCREMENT,
    Id_Paiement INT UNSIGNED NOT NULL,
    Montant DECIMAL(12, 2) NOT NULL,
    Devise CHAR(3) NOT NULL DEFAULT 'HTG',
    Reference_Externe VARCHAR(191) NOT NULL,
    Date_Remboursement DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Id_Administrateur INT UNSIGNED NOT NULL,
    PRIMARY KEY (Id_Remboursement),
    UNIQUE KEY UQ_Remboursement_Reference (Reference_Externe),
    KEY IX_Remboursement_Paiement (Id_Paiement),
    CONSTRAINT FK_Remboursement_Paiement
        FOREIGN KEY (Id_Paiement) REFERENCES Paiement (Id_Paiement)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT FK_Remboursement_Administrateur
        FOREIGN KEY (Id_Administrateur) REFERENCES Utilisateur (idUtilisateur)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT CK_Remboursement_Montant CHECK (Montant > 0)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS Historique_Operation (
    Id_Operation BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    Id_Utilisateur INT UNSIGNED NULL,
    Type_Operation VARCHAR(100) NOT NULL,
    Entite VARCHAR(60) NOT NULL,
    Id_Entite VARCHAR(64) NULL,
    Details JSON NULL,
    Date_Operation DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (Id_Operation),
    KEY IX_Historique_Utilisateur_Date (Id_Utilisateur, Date_Operation),
    KEY IX_Historique_Entite (Entite, Id_Entite),
    CONSTRAINT FK_Historique_Utilisateur
        FOREIGN KEY (Id_Utilisateur) REFERENCES Utilisateur (idUtilisateur)
        ON UPDATE CASCADE
        ON DELETE SET NULL
) ENGINE = InnoDB;
