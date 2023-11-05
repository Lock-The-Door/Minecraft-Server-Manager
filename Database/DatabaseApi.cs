using System.Data;
using Microsoft.Data.SqlClient;
using Minecraft_Server_Manager.CraftyControl;

namespace Minecraft_Server_Manager.Database;

class DatabaseApi
{
    public static DatabaseApi Instance { get; private set; } = new DatabaseApi(DatabaseConfiguration.Instance);
    public static void OverrideInstance(DatabaseConfiguration config) => Instance = new DatabaseApi(config);

    private readonly string? _connectionString;

    private DatabaseApi(DatabaseConfiguration config)
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = config.Source,
            Authentication = SqlAuthenticationMethod.SqlPassword,
            TrustServerCertificate = true,
            IntegratedSecurity = false,
            UserID = config.UserId,
            Password = config.Password,
        };
        _connectionString = builder.ConnectionString;
    }

    public async Task<List<MinecraftServerWrapper>> FilterMinecraftServers(List<MinecraftServerWrapper> mcServers, ulong userId, ulong? guildId, bool listAll = false)
    {
        Dictionary<Guid, bool> serverAccessRules = new();

        using (var connection = new SqlConnection(_connectionString))
        using (SqlDataAdapter adapter = new("SELECT server_uuid, is_ban FROM ServerAccessRules ", connection))
        {
            try
            {
                await connection.OpenAsync();
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return mcServers;
            }

            // Select all rules that apply to the user
            adapter.SelectCommand.CommandText += "WHERE discord_id = @user_id OR discord_id = @guild_id OR discord_id IS NULL";
            adapter.SelectCommand.Parameters.Add("@user_id", SqlDbType.Decimal, 19).Value = userId;
            adapter.SelectCommand.Parameters.Add("@guild_id", SqlDbType.Decimal, 19).Value = guildId.HasValue ? guildId : DBNull.Value;
            var reader = await adapter.SelectCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // TODO: Need to slightly modify so that by default, a guild rule must match unless in dms or a "list all" flag is set
                Guid serverUuid = await reader.IsDBNullAsync(0) ? Guid.Empty : reader.GetGuid(0);
                bool? isBan = await reader.IsDBNullAsync(1) ? null : reader.GetBoolean(1);

                if (!serverAccessRules.ContainsKey(serverUuid))
                    serverAccessRules[serverUuid] = isBan != true;
                else
                    serverAccessRules[serverUuid] &= isBan != true;
            }
        }

        return mcServers.FindAll(server =>
        {
            if (serverAccessRules.TryGetValue(server.UUID, out bool isAllowed))
                return isAllowed;
            else if (serverAccessRules.TryGetValue(Guid.Empty, out isAllowed))
                return isAllowed;
            else
                return false;
        });
    }

    public async Task<bool> IsUserAllowed(Guid serverUuid, ulong userId, ulong? guildId)
    {
        bool isAllowed = false;
    
        using (var connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand("dbo.IsUserAllowed", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@server_uuid", SqlDbType.UniqueIdentifier).Value = serverUuid;
            command.Parameters.Add("@user_id", SqlDbType.Decimal, 19).Value = userId;
            command.Parameters.Add("@guild_id", SqlDbType.Decimal, 19).Value = guildId.HasValue ? guildId : DBNull.Value;

            try
            {
                await connection.OpenAsync();
                isAllowed = (bool) (await command.ExecuteScalarAsync() ?? false);
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        return isAllowed;
    }
}