using System;
using System.IO;
using System.Text.Json;

namespace Project.Launch.Tools
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