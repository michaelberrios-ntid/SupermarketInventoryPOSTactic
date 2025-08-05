namespace Common;

using System;
using System.IO;
using Microsoft.Data.Sqlite;

public static class Database
{
    public const string POSLocalDB = "POS_Local.db";
    public const string LocalStoreDB = "LocalStore.db";

    public static void EnsurePOSSchema()
    {
        SqliteConnection connection = GetPOSLocalDB();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS SalesTransaction (
            id TEXT PRIMARY KEY,
            transaction_type TEXT NOT NULL CHECK(transaction_type IN ('Sale', 'Refund')),
            product_id TEXT NOT NULL,
            quantity INTEGER NOT NULL,
            price REAL NOT NULL,
            timestamp TEXT NOT NULL
        );";
        command.ExecuteNonQuery();
    }

    public static void RebuildPOSSchema()
    {

        SqliteConnection connection = GetPOSLocalDB();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        DROP TABLE IF EXISTS SalesTransaction;

        CREATE TABLE SalesTransaction (
            id TEXT PRIMARY KEY,
            transaction_type TEXT NOT NULL CHECK(transaction_type IN ('Sale', 'Refund')),
            product_id TEXT NOT NULL,
            quantity INTEGER NOT NULL,
            price REAL NOT NULL,
            timestamp TEXT NOT NULL
        );";
        command.ExecuteNonQuery();
    }

    public static void EnsureStoreSchema()
    {
        SqliteConnection connection = GetStoreLocalDB();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS Product (
            product_id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            price REAL NOT NULL,
            quantity INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS SalesTransaction (
            id TEXT PRIMARY KEY,
            transaction_type TEXT NOT NULL CHECK(transaction_type IN ('Sale', 'Refund')),
            product_id INTEGER NOT NULL,
            quantity INTEGER NOT NULL,
            price REAL NOT NULL,
            timestamp TEXT NOT NULL
        );";
        command.ExecuteNonQuery();
    }

    public static void RebuildStoreSchema()
    {
        SqliteConnection connection = GetStoreLocalDB();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        DROP TABLE IF EXISTS SalesTransaction;
        DROP TABLE IF EXISTS Product;

        CREATE TABLE Product (
            product_id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            price REAL NOT NULL,
            quantity INTEGER NOT NULL
        );

        CREATE TABLE SalesTransaction (
            id TEXT PRIMARY KEY,
            transaction_type TEXT NOT NULL CHECK(transaction_type IN ('Sale', 'Refund')),
            product_id INTEGER NOT NULL,
            quantity INTEGER NOT NULL,
            price REAL NOT NULL,
            timestamp TEXT NOT NULL
        );";
        command.ExecuteNonQuery();
    }

    public static void SeedStoreInventory()
    {
        SqliteConnection connection = GetStoreLocalDB();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT OR IGNORE INTO Product (product_id, name, price, quantity) VALUES
            (1001, 'Organic Apples', 0.99, 100),
            (1002, 'Whole Milk Gallon', 3.49, 50),
            (1003, 'Brown Eggs (12ct)', 2.79, 60),
            (1004, 'Wheat Bread Loaf', 1.99, 40),
            (1005, 'Bottled Water (24pk)', 4.99, 30);";
        command.ExecuteNonQuery();
    }


    private static string GetConnectionString(string dbFileName)
    {
        var dataPath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "./data";
        var fullPath = Path.Combine(dataPath, dbFileName);
        return $"Data Source={fullPath}";
    }

    public static SqliteConnection GetConnection(string dbFileName)
    {
        return new SqliteConnection(GetConnectionString(dbFileName));
    }

    public static SqliteConnection GetPOSLocalDB()
    {
        return new SqliteConnection(GetConnectionString(POSLocalDB));
    }

    public static SqliteConnection GetStoreLocalDB()
    {
        return new SqliteConnection(GetConnectionString(LocalStoreDB));
    }
}
