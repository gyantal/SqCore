using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using SqCommon;
using DbCommon;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DbManager;

class Controller
{
    static public Controller g_controller = new();

    internal static void Start()
    {
    }

    internal static void Exit()
    {
    }

    public static void TestLegacyDb()
    {
        string? legacySqlConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault");
        using var conn = new NpgsqlConnection(legacySqlConnString);
    }
}