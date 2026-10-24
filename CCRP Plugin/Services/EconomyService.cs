using System;
using System.Linq;
using System.Reflection;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    internal class EconomyService
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public bool TryGetBalance(string playerId, out decimal balance, out string errorKey)
        {
            balance = 0;
            errorKey = null;
            EconomyApi api;
            try
            {
                api = ResolveApi();
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not connect to an economy provider: {GetRootException(exception).Message}");
                errorKey = "BypassShopEconomyFailure";
                return false;
            }
            if (api == null)
            {
                errorKey = "BypassShopEconomyUnavailable";
                return false;
            }

            try
            {
                balance = ReadBalance(api, playerId);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not read {api.Name} balance for {playerId}: {GetRootException(exception).Message}");
                errorKey = "BypassShopEconomyFailure";
                return false;
            }
        }

        public bool TryChangeBalance(
            string playerId,
            decimal amount,
            out decimal previousBalance,
            out decimal currentBalance,
            out string errorKey)
        {
            previousBalance = 0;
            currentBalance = 0;
            errorKey = null;
            EconomyApi api;
            try
            {
                api = ResolveApi();
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not connect to an economy provider: {GetRootException(exception).Message}");
                errorKey = "BypassShopEconomyFailure";
                return false;
            }
            if (api == null)
            {
                errorKey = "BypassShopEconomyUnavailable";
                return false;
            }

            try
            {
                previousBalance = ReadBalance(api, playerId);
                currentBalance = previousBalance;
                if (amount < 0 && previousBalance < -amount)
                {
                    errorKey = "BypassShopInsufficientFunds";
                    return false;
                }

                object result = api.ChangeBalance.Invoke(api.Database, api.ChangeBalanceArguments(playerId, amount));
                if (result is System.Threading.Tasks.Task)
                {
                    Logger.LogError($"{api.Name} exposed an asynchronous balance method that cannot be used by the synchronous Rocket command.");
                    errorKey = "BypassShopEconomyFailure";
                    return false;
                }

                currentBalance = ReadBalance(api, playerId);
                if ((result is bool && !(bool)result) || currentBalance != previousBalance + amount)
                {
                    Logger.LogError(
                        $"{api.Name} balance change for {playerId} did not match the requested amount. " +
                        $"Expected {previousBalance + amount}, got {currentBalance}.");
                    errorKey = "BypassShopEconomyFailure";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    currentBalance = ReadBalance(api, playerId);
                }
                catch (Exception readException)
                {
                    Logger.LogError($"{api.Name} balance for {playerId} could not be rechecked after a failed transaction: {GetRootException(readException).Message}");
                }

                Logger.LogError($"Could not change {api.Name} balance for {playerId}: {GetRootException(exception).Message}");
                errorKey = "BypassShopEconomyFailure";
                return false;
            }
        }

        private static decimal ReadBalance(EconomyApi api, string playerId)
        {
            object result = api.GetBalance.Invoke(api.Database, new object[] { playerId });
            if (result is System.Threading.Tasks.Task)
            {
                throw new InvalidOperationException("The economy balance API is asynchronous.");
            }
            if (result == null)
            {
                throw new InvalidOperationException("The economy balance API returned no balance.");
            }

            return Convert.ToDecimal(result, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static EconomyApi ResolveApi()
        {
            EconomyApi advancedApi = TryResolveAdvancedEconomy();
            return advancedApi ?? TryResolveUconomy();
        }

        private static EconomyApi TryResolveAdvancedEconomy()
        {
            Type pluginType = FindType("RestoreMonarchy.AdvancedEconomy.AdvancedEconomyPlugin");
            if (pluginType == null)
            {
                return null;
            }

            object plugin = GetStaticMember(pluginType, "Instance");
            object database = GetInstanceMember(plugin, "Database");
            if (database == null)
            {
                return null;
            }

            MethodInfo getBalance = FindMethod(database.GetType(), "GetPlayerBalance", typeof(string));
            MethodInfo changeBalance = FindAdvancedChangeMethod(database.GetType());
            return getBalance == null || changeBalance == null ||
                   typeof(System.Threading.Tasks.Task).IsAssignableFrom(getBalance.ReturnType) ||
                   typeof(System.Threading.Tasks.Task).IsAssignableFrom(changeBalance.ReturnType)
                ? null
                : new EconomyApi("AdvancedEconomy", database, getBalance, changeBalance);
        }

        private static EconomyApi TryResolveUconomy()
        {
            Type pluginType = FindType("fr34kyn01535.Uconomy.Uconomy");
            if (pluginType == null)
            {
                return null;
            }

            object plugin = GetStaticMember(pluginType, "Instance");
            object database = GetInstanceMember(plugin, "Database");
            if (database == null)
            {
                return null;
            }

            MethodInfo getBalance = FindMethod(database.GetType(), "GetBalance", typeof(string));
            MethodInfo changeBalance = FindMethod(database.GetType(), "IncreaseBalance", typeof(string), typeof(decimal));
            return getBalance == null || changeBalance == null
                ? null
                : new EconomyApi("Uconomy", database, getBalance, changeBalance);
        }

        private static MethodInfo FindAdvancedChangeMethod(Type databaseType)
        {
            return FindMethod(
                       databaseType,
                       "IncreasePlayerBalance",
                       typeof(string),
                       typeof(decimal),
                       typeof(DateTime)) ??
                   FindMethod(databaseType, "IncreasePlayerBalance", typeof(string), typeof(decimal));
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            return type.GetMethods(InstanceFlags).FirstOrDefault(method =>
                string.Equals(method.Name, name, StringComparison.Ordinal) &&
                method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static object GetStaticMember(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, StaticFlags);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            FieldInfo field = type.GetField(name, StaticFlags);
            return field == null ? null : field.GetValue(null);
        }

        private static object GetInstanceMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            PropertyInfo property = instance.GetType().GetProperty(name, InstanceFlags);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = instance.GetType().GetField(name, InstanceFlags);
            return field == null ? null : field.GetValue(instance);
        }

        private static Exception GetRootException(Exception exception)
        {
            TargetInvocationException invocationException = exception as TargetInvocationException;
            while (invocationException != null && invocationException.InnerException != null)
            {
                exception = invocationException.InnerException;
                invocationException = exception as TargetInvocationException;
            }

            return exception;
        }

        private sealed class EconomyApi
        {
            public string Name { get; private set; }
            public object Database { get; private set; }
            public MethodInfo GetBalance { get; private set; }
            public MethodInfo ChangeBalance { get; private set; }

            public EconomyApi(string name, object database, MethodInfo getBalance, MethodInfo changeBalance)
            {
                Name = name;
                Database = database;
                GetBalance = getBalance;
                ChangeBalance = changeBalance;
            }

            public object[] ChangeBalanceArguments(string playerId, decimal amount)
            {
                ParameterInfo[] parameters = ChangeBalance.GetParameters();
                return parameters.Length == 3
                    ? new object[] { playerId, amount, DateTime.UtcNow }
                    : new object[] { playerId, amount };
            }
        }
    }
}
