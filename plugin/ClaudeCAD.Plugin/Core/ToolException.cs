using System;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>Standard error codes of the IPC protocol (spec §6).</summary>
    public static class ErrorCodes
    {
        public const string InvalidParams = "INVALID_PARAMS";
        public const string EntityNotFound = "ENTITY_NOT_FOUND";
        public const string LayerNotFound = "LAYER_NOT_FOUND";
        public const string BlockNotFound = "BLOCK_NOT_FOUND";
        public const string Locked = "LOCKED";
        public const string AcadError = "ACAD_ERROR";
        public const string Timeout = "TIMEOUT";
        public const string TooManyEntities = "TOO_MANY_ENTITIES";
        public const string UnknownTool = "UNKNOWN_TOOL";
        public const string ReadOnly = "READ_ONLY";
    }

    /// <summary>
    /// An error that is reported back to Claude as an error envelope. The message
    /// must tell Claude how to fix the problem (which tool to call next).
    /// </summary>
    public class ToolException : Exception
    {
        public string Code { get; }

        public ToolException(string code, string message) : base(message)
        {
            Code = code;
        }

        public static ToolException Invalid(string message) => new ToolException(ErrorCodes.InvalidParams, message);
    }

    /// <summary>System-level failure: aborts a whole batch_execute (spec §7.8).</summary>
    public sealed class FatalToolException : ToolException
    {
        public FatalToolException(string message) : base(ErrorCodes.AcadError, message) { }
    }
}
