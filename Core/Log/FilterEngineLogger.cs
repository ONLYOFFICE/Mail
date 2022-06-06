namespace ASC.Mail.Core.Log
{
    internal static partial class FilterEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "{err}")]
        public static partial void ErrorFilterEngine(this ILogger logger, string err);

        [LoggerMessage(Level = LogLevel.Information, Message = "Filter condition succeed -> {0} {1} '{2}'")]
        public static partial void InfoFilterEngineConditionSucceed(this ILogger logger, string key, string operation, string value);

        [LoggerMessage(Level = LogLevel.Information, Message = "Skip filter by not match all conditions")]
        public static partial void InfoFilterEngineSkipFilter(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Unknown MatchMultiConditionsType")]
        public static partial void ErrorFilterEngineUnknownMatchMultiConditionsType(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Apply filter (id={filterId}) action: '{action}' id = {data}")]
        public static partial void InfoFilterEngineApplyFilterWithData(this ILogger logger, int filterId, string action, string data);

        [LoggerMessage(Level = LogLevel.Information, Message = "Apply filter (id={filterId}) action: '{action}'")]
        public static partial void InfoFilterEngineApplyFilter(this ILogger logger, int filterId, string action);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Disable filter with id={filterId}")]
        public static partial void DebugFilterEngineDisableFilter(this ILogger logger, int filterId);

        [LoggerMessage(Level = LogLevel.Error, Message = "ApplyFilters(filterId = {filterId}, mailId = {messageId}) Exception:\r\n{errMsg}\r\n")]
        public static partial void ErrorFilterEngineApplyFilters(this ILogger logger, int filterId, int messageId, string errMsg);
    }
}
