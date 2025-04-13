using AllaganLib.GameSheets.Sheets;
using Lumina.Excel.Sheets;
using Lumina.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using CriticalCommonLib.Models;
using CriticalCommonLib.Enums;
using Autofac;
using Lumina;
using Dalamud.Plugin;
using System.IO;
using ECommons.Logging;
using Dalamud.Plugin.Services;

namespace CriticalCommonLib.SQLite
{
    public class InventoryDatabase : IDisposable
    {
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly SqliteConnection connection;
        private readonly IComponentContext componentContext;
        private readonly GameData gameData;
        private bool disposed = false;

        private string DatabasePath
        {
            get
            {
                var pluginRootDirectory = Path.GetDirectoryName(pluginInterface.ConfigDirectory.FullName) ?? string.Empty;

                var sharedDir = Path.Combine(pluginRootDirectory, "InventoryTools");

                return Path.Combine(sharedDir, "InventoryDatabase.db");
            }
        }

        public InventoryDatabase(
            IDalamudPluginInterface pluginInterface,
            IComponentContext componentContext, 
            IPluginLog pluginLog,
            GameData gameData)
        {
            this.componentContext = componentContext;
            this.gameData = gameData;
            this.pluginInterface = pluginInterface;

            // 创建数据库文件
            if (!Directory.Exists(Path.GetDirectoryName(DatabasePath)))
            {
                throw new DirectoryNotFoundException("共享数据库目录未找到");
            }
            else
            {
                // 初始化SQLite
                SQLitePCL.Batteries.Init();
                connection = new SqliteConnection($"Data Source={DatabasePath}");
                InitializeDatabase();
            }
        }

        private void InitializeDatabase()
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS InventoryItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Container INTEGER,
                Slot INTEGER,
                ItemId INTEGER,
                Quantity INTEGER,
                Spiritbond INTEGER,
                Condition INTEGER,
                Flags INTEGER,
                Materia0 INTEGER,
                Materia1 INTEGER,
                Materia2 INTEGER,
                Materia3 INTEGER,
                Materia4 INTEGER,
                MateriaLevel0 INTEGER,
                MateriaLevel1 INTEGER,
                MateriaLevel2 INTEGER,
                MateriaLevel3 INTEGER,
                MateriaLevel4 INTEGER,
                Stain INTEGER,
                Stain2 INTEGER,
                GlamourId INTEGER,
                SortedContainer INTEGER,
                SortedCategory INTEGER,
                SortedSlotIndex INTEGER,
                RetainerId INTEGER,
                RetainerMarketPrice INTEGER,
                GearSets TEXT,
                GearSetNames TEXT
            );";
            command.ExecuteNonQuery();
        }

        public void SaveInventoryItems(List<InventoryItem> items)
        {
            using var transaction = connection.BeginTransaction();

            try
            {
                // 清空旧数据
                var clearCommand = connection.CreateCommand();
                clearCommand.CommandText = "DELETE FROM InventoryItems";
                clearCommand.ExecuteNonQuery();

                // 批量插入新数据
                foreach (var item in items)
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = @"
                    INSERT INTO InventoryItems (
                        Container, Slot, ItemId, Quantity, Spiritbond, Condition, Flags,
                        Materia0, Materia1, Materia2, Materia3, Materia4,
                        MateriaLevel0, MateriaLevel1, MateriaLevel2, MateriaLevel3, MateriaLevel4,
                        Stain, Stain2, GlamourId, SortedContainer, SortedCategory,
                        SortedSlotIndex, RetainerId, RetainerMarketPrice, 
                        GearSets, GearSetNames
                    ) VALUES (
                        @Container, @Slot, @ItemId, @Quantity, @Spiritbond, @Condition, @Flags,
                        @Materia0, @Materia1, @Materia2, @Materia3, @Materia4,
                        @MateriaLevel0, @MateriaLevel1, @MateriaLevel2, @MateriaLevel3, @MateriaLevel4,
                        @Stain, @Stain2, @GlamourId, @SortedContainer, @SortedCategory,
                        @SortedSlotIndex, @RetainerId, @RetainerMarketPrice,
                        @GearSets, @GearSetNames
                    )";

                    // 为每个参数赋值
                    insertCommand.Parameters.AddWithValue("@Container", (int)item.Container);
                    insertCommand.Parameters.AddWithValue("@Slot", item.Slot);
                    insertCommand.Parameters.AddWithValue("@ItemId", item.ItemId);
                    insertCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                    insertCommand.Parameters.AddWithValue("@Spiritbond", item.Spiritbond);
                    insertCommand.Parameters.AddWithValue("@Condition", item.Condition);
                    insertCommand.Parameters.AddWithValue("@Flags", (int)item.Flags);
                    insertCommand.Parameters.AddWithValue("@Materia0", item.Materia0);
                    insertCommand.Parameters.AddWithValue("@Materia1", item.Materia1);
                    insertCommand.Parameters.AddWithValue("@Materia2", item.Materia2);
                    insertCommand.Parameters.AddWithValue("@Materia3", item.Materia3);
                    insertCommand.Parameters.AddWithValue("@Materia4", item.Materia4);
                    insertCommand.Parameters.AddWithValue("@MateriaLevel0", item.MateriaLevel0);
                    insertCommand.Parameters.AddWithValue("@MateriaLevel1", item.MateriaLevel1);
                    insertCommand.Parameters.AddWithValue("@MateriaLevel2", item.MateriaLevel2);
                    insertCommand.Parameters.AddWithValue("@MateriaLevel3", item.MateriaLevel3);
                    insertCommand.Parameters.AddWithValue("@MateriaLevel4", item.MateriaLevel4);
                    insertCommand.Parameters.AddWithValue("@Stain", item.Stain);
                    insertCommand.Parameters.AddWithValue("@Stain2", item.Stain2);
                    insertCommand.Parameters.AddWithValue("@GlamourId", item.GlamourId);
                    insertCommand.Parameters.AddWithValue("@SortedContainer", (int)item.SortedContainer);
                    insertCommand.Parameters.AddWithValue("@SortedCategory", (int)item.SortedCategory);
                    insertCommand.Parameters.AddWithValue("@SortedSlotIndex", item.SortedSlotIndex);
                    insertCommand.Parameters.AddWithValue("@RetainerId", item.RetainerId);
                    insertCommand.Parameters.AddWithValue("@RetainerMarketPrice", item.RetainerMarketPrice);
                    
                    // 对于复杂类型需要序列化
                    string gearSetsJson = item.GearSets != null ? 
                        JsonConvert.SerializeObject(item.GearSets) : "";
                    insertCommand.Parameters.AddWithValue("@GearSets", gearSetsJson);
                    
                    string gearSetNamesJson = item.GearSetNames != null ? 
                        JsonConvert.SerializeObject(item.GearSetNames) : "";
                    insertCommand.Parameters.AddWithValue("@GearSetNames", gearSetNamesJson);

                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public List<InventoryItem> LoadInventoryItems()
        {
            var items = new List<InventoryItem>();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM InventoryItems";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // 使用依赖注入创建InventoryItem实例
                var item = componentContext.Resolve<InventoryItem>();

                // 从数据库读取并赋值各个字段
                item.Container = (InventoryType)reader.GetInt32(reader.GetOrdinal("Container"));
                item.Slot = (short)reader.GetInt32(reader.GetOrdinal("Slot"));
                item.ItemId = (uint)reader.GetInt32(reader.GetOrdinal("ItemId"));
                item.Quantity = (uint)reader.GetInt32(reader.GetOrdinal("Quantity"));
                item.Spiritbond = (ushort)reader.GetInt32(reader.GetOrdinal("Spiritbond"));
                item.Condition = (ushort)reader.GetInt32(reader.GetOrdinal("Condition"));
                item.Flags = (FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags)reader.GetInt32(reader.GetOrdinal("Flags"));
                item.Materia0 = (ushort)reader.GetInt32(reader.GetOrdinal("Materia0"));
                item.Materia1 = (ushort)reader.GetInt32(reader.GetOrdinal("Materia1"));
                item.Materia2 = (ushort)reader.GetInt32(reader.GetOrdinal("Materia2"));
                item.Materia3 = (ushort)reader.GetInt32(reader.GetOrdinal("Materia3"));
                item.Materia4 = (ushort)reader.GetInt32(reader.GetOrdinal("Materia4"));
                item.MateriaLevel0 = (byte)reader.GetInt32(reader.GetOrdinal("MateriaLevel0"));
                item.MateriaLevel1 = (byte)reader.GetInt32(reader.GetOrdinal("MateriaLevel1"));
                item.MateriaLevel2 = (byte)reader.GetInt32(reader.GetOrdinal("MateriaLevel2"));
                item.MateriaLevel3 = (byte)reader.GetInt32(reader.GetOrdinal("MateriaLevel3"));
                item.MateriaLevel4 = (byte)reader.GetInt32(reader.GetOrdinal("MateriaLevel4"));
                item.Stain = (byte)reader.GetInt32(reader.GetOrdinal("Stain"));
                item.Stain2 = (byte)reader.GetInt32(reader.GetOrdinal("Stain2"));
                item.GlamourId = (uint)reader.GetInt32(reader.GetOrdinal("GlamourId"));
                item.SortedContainer = (InventoryType)reader.GetInt32(reader.GetOrdinal("SortedContainer"));
                item.SortedCategory = (InventoryCategory)reader.GetInt32(reader.GetOrdinal("SortedCategory"));
                item.SortedSlotIndex = reader.GetInt32(reader.GetOrdinal("SortedSlotIndex"));
                item.RetainerId = (ulong)reader.GetInt64(reader.GetOrdinal("RetainerId"));
                item.RetainerMarketPrice = (uint)reader.GetInt32(reader.GetOrdinal("RetainerMarketPrice"));

                // 反序列化复杂类型
                var gearSetsStr = reader.GetString(reader.GetOrdinal("GearSets"));
                if (!string.IsNullOrEmpty(gearSetsStr))
                {
                    item.GearSets = JsonConvert.DeserializeObject<uint[]>(gearSetsStr);
                }

                var gearSetNamesStr = reader.GetString(reader.GetOrdinal("GearSetNames"));
                if (!string.IsNullOrEmpty(gearSetNamesStr))
                {
                    item.GearSetNames = JsonConvert.DeserializeObject<string[]>(gearSetNamesStr);
                }

                // 如果提供了游戏数据，填充相关数据
                if (gameData != null)
                {
                    item.PopulateData(gameData.Excel, gameData.Options.DefaultExcelLanguage);
                }

                items.Add(item);
            }

            return items;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // 释放托管资源
                connection?.Close();
                connection?.Dispose();
            }

            // 此处释放非托管资源 (本例中没有)

            disposed = true;
        }
    }
}
