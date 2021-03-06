﻿
CREATE TABLE IF NOT EXISTS PerfomanceCounterValues (
	AppId SMALLINT NOT NULL DEFAULT '0',
	CounterId SMALLINT NOT NULL,
	Timestamp DATETIME NOT NULL,
	Value FLOAT NULL DEFAULT NULL,
	PRIMARY KEY (AppId, CounterId, Timestamp)
);

CREATE TABLE IF NOT EXISTS Applications (
	AppId SMALLINT NULL DEFAULT '0',
	AppName VARCHAR(255) NULL DEFAULT NULL
);

CREATE TABLE IF NOT EXISTS CounterNames (
	Id SMALLINT NOT NULL DEFAULT '0',
	Name VARCHAR(255) NULL DEFAULT NULL,
	PRIMARY KEY (Id)
);