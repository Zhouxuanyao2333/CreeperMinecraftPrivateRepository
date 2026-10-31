using System;
using System.IO;
using System.Text.Json;

using Project.Launch.Tools.JavaConfig;
using Project.Launch.Tools.LogsConfig;
namespace Project.Launch.Auth
{
    public static class AccountService
    {
        // ★ P1-5 修复：路径统一走 LauncherPaths，不再自己拼一份
        private static readonly object _lock = new object();

        public static AccountsRoot LoadAccounts()
        {
            lock (_lock)
            {
                if (!File.Exists(LauncherPaths.AccountsFile))
                    return new AccountsRoot();

                try
                {
                    string json = File.ReadAllText(LauncherPaths.AccountsFile);
                    return JsonSerializer.Deserialize<AccountsRoot>(json) ?? new AccountsRoot();
                }
                catch (Exception ex)
                {
                    LogHelper.Write(LauncherPaths.AppLog,
                        $"读取账户失败: {ex.Message}",
                        $"Failed to load accounts: {ex.Message}");
                    return new AccountsRoot();
                }
            }
        }

        // ★ P0-5 修复：写 tmp + Move 覆盖 + try-catch
        public static void SaveAccounts(AccountsRoot root)
        {
            lock (_lock)
            {
                SaveAccountsInternal(root);
            }
        }

        // ★ P1-1 修复：read-modify-write 收口在单个 lock 内
        private static void ModifyAccounts(Action<AccountsRoot> mutate)
        {
            lock (_lock)
            {
                AccountsRoot root;
                if (!File.Exists(LauncherPaths.AccountsFile))
                {
                    root = new AccountsRoot();
                }
                else
                {
                    try
                    {
                        string json = File.ReadAllText(LauncherPaths.AccountsFile);
                        root = JsonSerializer.Deserialize<AccountsRoot>(json) ?? new AccountsRoot();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Write(LauncherPaths.AppLog,
                            $"读取账户失败（将重置）: {ex.Message}",
                            $"Failed to load accounts (will reset): {ex.Message}");
                        root = new AccountsRoot();
                    }
                }

                mutate(root);
                SaveAccountsInternal(root);
            }
        }

        private static void SaveAccountsInternal(AccountsRoot root)
        {
            try
            {
                string json = JsonSerializer.Serialize(root,
                    new JsonSerializerOptions { WriteIndented = true });

                // 原子写：先写临时文件，再 Move 覆盖
                string tmpPath = LauncherPaths.AccountsFile + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, LauncherPaths.AccountsFile, overwrite: true);
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"保存账户失败: {ex.Message}",
                    $"Failed to save accounts: {ex.Message}");
            }
        }

        public static AccountInfo? GetSelectedAccount()
        {
            var root = LoadAccounts();
            if (string.IsNullOrEmpty(root.SelectedAccount) || !root.Accounts.ContainsKey(root.SelectedAccount))
                return null;
            return root.Accounts[root.SelectedAccount];
        }

        /// <summary>
        /// 同时返回选中账户的 key 与账户本体。
        /// 刷新 token 后需要知道 key 才能精确更新（而不是整对象替换）。
        /// </summary>
        public static (string id, AccountInfo account)? GetSelectedAccountWithId()
        {
            var root = LoadAccounts();
            if (string.IsNullOrEmpty(root.SelectedAccount) || !root.Accounts.ContainsKey(root.SelectedAccount))
                return null;
            return (root.SelectedAccount, root.Accounts[root.SelectedAccount]);
        }

        public static void SetSelectedAccount(string accountId, AccountInfo account)
        {
            ModifyAccounts(root =>
            {
                root.Accounts[accountId] = account;
                root.SelectedAccount = accountId;
            });
        }

        public static void RemoveAccount(string accountId)
        {
            ModifyAccounts(root =>
            {
                if (root.Accounts.Remove(accountId) && root.SelectedAccount == accountId)
                    root.SelectedAccount = string.Empty;
            });
        }

        /// <summary>
        /// 静默刷新成功后，精确更新微软账户的 token 相关字段并持久化。
        ///
        /// 只覆盖 token 相关字段，不动其他账户、不改 UI 状态。
        /// 若按 UUID 推导出的 key 不存在（理论上不该发生），则新增该账户并设为选中。
        /// </summary>
        public static void UpdateMicrosoftTokens(AccountInfo refreshed)
        {
            if (refreshed == null) throw new ArgumentNullException(nameof(refreshed));

            string accountId = $"microsoft_{refreshed.Uuid}";

            ModifyAccounts(root =>
            {
                if (root.Accounts.TryGetValue(accountId, out var existing))
                {
                    existing.Username = refreshed.Username;
                    existing.Uuid = refreshed.Uuid;
                    existing.MicrosoftRefreshToken = refreshed.MicrosoftRefreshToken;
                    existing.MinecraftAccessToken = refreshed.MinecraftAccessToken;
                    existing.TokenExpireAt = refreshed.TokenExpireAt;
                    existing.SkinUrl = refreshed.SkinUrl;
                    existing.LastUsed = refreshed.LastUsed;
                }
                else
                {
                    root.Accounts[accountId] = refreshed;
                }

                root.SelectedAccount = accountId;
            });
        }

        public static bool GetLoginCheck()
        {
            var root = LoadAccounts();
            return root.LoginCheck;
        }

        public static void SetLoginCheck(bool value)
        {
            ModifyAccounts(root => root.LoginCheck = value);
        }
    }
}