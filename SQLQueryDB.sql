-- ============================================================
-- База данных: Тестирование студентов
-- Префикс таблиц: UP03_
-- СУБД: Microsoft SQL Server
-- ============================================================

-- Создание базы данных (при необходимости)
-- CREATE DATABASE TestingStudents;
-- GO

-- USE TestingStudents;
-- GO

-- ============================================================
-- 1. Таблица Role (Роли пользователей)
-- ============================================================
CREATE TABLE UP03_Role (
    IDRole          INT IDENTITY(1,1) NOT NULL,
    NazvanieRoli    VARCHAR(50)       NOT NULL,
    
    CONSTRAINT PK_UP03_Role PRIMARY KEY (IDRole),
    CONSTRAINT UQ_UP03_Role_NazvanieRoli UNIQUE (NazvanieRoli)
);
GO

-- ============================================================
-- 2. Таблица Users (Пользователи)
-- ============================================================
CREATE TABLE UP03_Users (
    IDUser          INT IDENTITY(1,1) NOT NULL,
    IDRole          INT               NOT NULL,
    Familiya        VARCHAR(100)      NOT NULL,
    Imya            VARCHAR(100)      NOT NULL,
    Otchestvo       VARCHAR(100)      NULL,
    Login           VARCHAR(50)       NOT NULL,
    DataSozdaniya   DATE              NOT NULL DEFAULT GETDATE(),
    
    CONSTRAINT PK_UP03_Users PRIMARY KEY (IDUser),
    CONSTRAINT UQ_UP03_Users_Login UNIQUE (Login),
    CONSTRAINT FK_UP03_Users_Role FOREIGN KEY (IDRole) 
        REFERENCES UP03_Role(IDRole)
);
GO

CREATE INDEX IX_UP03_Users_IDRole ON UP03_Users(IDRole);
GO

-- ============================================================
-- 3. Таблица Groups (Учебные группы)
-- ============================================================
CREATE TABLE UP03_Groups (
    IDGroup         INT IDENTITY(1,1) NOT NULL,
    Nazvanie        VARCHAR(50)       NOT NULL,
    Kurs            INT               NOT NULL,
    Otdelenie       VARCHAR(50)       NULL,
    
    CONSTRAINT PK_UP03_Groups PRIMARY KEY (IDGroup)
);
GO

-- ============================================================
-- 4. Таблица StudentGroup (Зачисление студентов в группы)
-- ============================================================
CREATE TABLE UP03_StudentGroup (
    IDStudentGroups INT IDENTITY(1,1) NOT NULL,
    IDStudenta      INT               NOT NULL,
    IDGroup         INT               NOT NULL,
    
    CONSTRAINT PK_UP03_StudentGroup PRIMARY KEY (IDStudentGroups),
    CONSTRAINT FK_UP03_StudentGroup_Users FOREIGN KEY (IDStudenta) 
        REFERENCES UP03_Users(IDUser),
    CONSTRAINT FK_UP03_StudentGroup_Groups FOREIGN KEY (IDGroup) 
        REFERENCES UP03_Groups(IDGroup),
    CONSTRAINT UQ_UP03_StudentGroup_Student_Group UNIQUE (IDStudenta, IDGroup)
);
GO

CREATE INDEX IX_UP03_StudentGroup_IDStudenta ON UP03_StudentGroup(IDStudenta);
CREATE INDEX IX_UP03_StudentGroup_IDGroup ON UP03_StudentGroup(IDGroup);
GO

-- ============================================================
-- 5. Таблица Predmet (Дисциплины)
-- ============================================================
CREATE TABLE UP03_Predmet (
    IDPredmet       INT IDENTITY(1,1) NOT NULL,
    Nazvanie        VARCHAR(50)       NOT NULL,
    Opisanie        VARCHAR(50)       NULL,
    
    CONSTRAINT PK_UP03_Predmet PRIMARY KEY (IDPredmet)
);
GO

-- ============================================================
-- 6. Таблица PrepodPredmet (Назначение преподавателей на дисциплины)
-- ============================================================
CREATE TABLE UP03_PrepodPredmet (
    IDPrepodPredmet     INT IDENTITY(1,1) NOT NULL,
    IDPrepodavatelya    INT               NOT NULL,
    IDPredmet           INT               NOT NULL,
    
    CONSTRAINT PK_UP03_PrepodPredmet PRIMARY KEY (IDPrepodPredmet),
    CONSTRAINT FK_UP03_PrepodPredmet_Users FOREIGN KEY (IDPrepodavatelya) 
        REFERENCES UP03_Users(IDUser),
    CONSTRAINT FK_UP03_PrepodPredmet_Predmet FOREIGN KEY (IDPredmet) 
        REFERENCES UP03_Predmet(IDPredmet),
    CONSTRAINT UQ_UP03_PrepodPredmet_Teacher_Subject UNIQUE (IDPrepodavatelya, IDPredmet)
);
GO

CREATE INDEX IX_UP03_PrepodPredmet_IDPrepodavatelya ON UP03_PrepodPredmet(IDPrepodavatelya);
CREATE INDEX IX_UP03_PrepodPredmet_IDPredmet ON UP03_PrepodPredmet(IDPredmet);
GO

-- ============================================================
-- 7. Таблица Tests (Тесты)
-- ============================================================
CREATE TABLE UP03_Tests (
    IDTest              INT IDENTITY(1,1) NOT NULL,
    IDPredmet           INT               NOT NULL,
    Nazvanie            VARCHAR(50)       NOT NULL,
    Opisanie            VARCHAR(50)       NULL,
    Vremya_vipolneniya  INT               NULL,
    
    CONSTRAINT PK_UP03_Tests PRIMARY KEY (IDTest),
    CONSTRAINT FK_UP03_Tests_Predmet FOREIGN KEY (IDPredmet) 
        REFERENCES UP03_Predmet(IDPredmet)
);
GO

CREATE INDEX IX_UP03_Tests_IDPredmet ON UP03_Tests(IDPredmet);
GO

-- ============================================================
-- 8. Таблица Voprosi (Вопросы)
-- ============================================================
CREATE TABLE UP03_Voprosi (
    IDVoprosa       INT IDENTITY(1,1) NOT NULL,
    IDTest          INT               NOT NULL,
    TextVoprosa     VARCHAR(50)       NOT NULL,
    TipVoprosa      VARCHAR(50)       NOT NULL,
    Kol_vo_ballov   INT               NOT NULL DEFAULT 1,
    
    CONSTRAINT PK_UP03_Voprosi PRIMARY KEY (IDVoprosa),
    CONSTRAINT FK_UP03_Voprosi_Tests FOREIGN KEY (IDTest) 
        REFERENCES UP03_Tests(IDTest),
    CONSTRAINT CK_UP03_Voprosi_TipVoprosa 
        CHECK (TipVoprosa IN ('Single', 'Multiple'))
);
GO

CREATE INDEX IX_UP03_Voprosi_IDTest ON UP03_Voprosi(IDTest);
GO

-- ============================================================
-- 9. Таблица Otvet (Варианты ответов)
-- ============================================================
CREATE TABLE UP03_Otvet (
    IDOtverta       INT IDENTITY(1,1) NOT NULL,
    IDVoprosa       INT               NOT NULL,
    TextOtverta     VARCHAR(500)      NOT NULL,
    isCorrect       BIT               NOT NULL DEFAULT 0,
    
    CONSTRAINT PK_UP03_Otvet PRIMARY KEY (IDOtverta),
    CONSTRAINT FK_UP03_Otvet_Voprosi FOREIGN KEY (IDVoprosa) 
        REFERENCES UP03_Voprosi(IDVoprosa)
);
GO

CREATE INDEX IX_UP03_Otvet_IDVoprosa ON UP03_Otvet(IDVoprosa);
GO

-- ============================================================
-- 10. Таблица Resultat (Результаты тестирования)
-- ============================================================
CREATE TABLE UP03_Resultat (
    IDResultat      INT IDENTITY(1,1) NOT NULL,
    IDUser          INT               NOT NULL,
    Balli           INT               NOT NULL DEFAULT 0,
    Procent         DECIMAL(5,2)      NOT NULL DEFAULT 0.00,
    Ocenka          INT               NULL,
    
    CONSTRAINT PK_UP03_Resultat PRIMARY KEY (IDResultat),
    CONSTRAINT FK_UP03_Resultat_Users FOREIGN KEY (IDUser) 
        REFERENCES UP03_Users(IDUser),
    CONSTRAINT CK_UP03_Resultat_Ocenka 
        CHECK (Ocenka IS NULL OR Ocenka IN (2, 3, 4, 5)),
    CONSTRAINT CK_UP03_Resultat_Procent 
        CHECK (Procent >= 0.00 AND Procent <= 100.00)
);
GO

CREATE INDEX IX_UP03_Resultat_IDUser ON UP03_Resultat(IDUser);
GO