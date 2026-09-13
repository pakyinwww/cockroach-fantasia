using System;
using Unity.Services.Multiplayer;

namespace CockroachFantasia.Networking
{
    public static class SessionFailureMapper
    {
        public static SessionFailureKind Map(Exception exception)
        {
            if (exception is SessionException sessionException)
            {
                switch (sessionException.Error)
                {
                    case SessionError.InvalidParameter:
                    case SessionError.InvalidSessionIdentifier:
                        return SessionFailureKind.InvalidCode;
                    case SessionError.SessionNotFound:
                    case SessionError.SessionDeleted:
                    case SessionError.AllocationNotFound:
                        return SessionFailureKind.Expired;
                    case SessionError.NotAuthorized:
                    case SessionError.Forbidden:
                        return SessionFailureKind.Unauthorized;
                    case SessionError.QoSMeasurementFailed:
                    case SessionError.NetworkManagerStartFailed:
                    case SessionError.NetworkSetupFailed:
                    case SessionError.TransportComponentMissing:
                    case SessionError.TransportInvalid:
                    case SessionError.RateLimitExceeded:
                        return SessionFailureKind.Unreachable;
                }
            }

            var message = FlattenMessages(exception).ToLowerInvariant();
            if (message.Contains("full") || message.Contains("maximum players") || message.Contains("no open slots"))
            {
                return SessionFailureKind.Full;
            }

            if (message.Contains("locked"))
            {
                return SessionFailureKind.Locked;
            }

            if (message.Contains("not found") || message.Contains("expired") || message.Contains("does not exist") ||
                message.Contains("failed to join allocation"))
            {
                return SessionFailureKind.Expired;
            }

            if (message.Contains("invalid") && (message.Contains("code") || message.Contains("session")))
            {
                return SessionFailureKind.InvalidCode;
            }

            if (exception is TimeoutException || message.Contains("timeout") || message.Contains("network") ||
                message.Contains("unreachable") || message.Contains("connection"))
            {
                return SessionFailureKind.Unreachable;
            }

            return SessionFailureKind.Unknown;
        }

        public static string ToUserMessage(SessionFailureKind kind)
        {
            return kind switch
            {
                SessionFailureKind.InvalidCode => "That room code is not valid. Check it and try again.",
                SessionFailureKind.Expired => "That room has expired or no longer exists.",
                SessionFailureKind.Full => "That room is full (4/4).",
                SessionFailureKind.Locked => "That match has already started. Ask the host to return to the lobby.",
                SessionFailureKind.Unreachable => "Could not reach the room. Check your connection and retry.",
                SessionFailureKind.HostLeft => "The host left, so the room closed.",
                SessionFailureKind.Disconnected => "You were disconnected from the room.",
                SessionFailureKind.Unauthorized => "This player is not allowed to join that room.",
                _ => "The room connection failed. Please retry."
            };
        }

        private static string FlattenMessages(Exception exception)
        {
            var message = string.Empty;
            for (var current = exception; current != null; current = current.InnerException)
            {
                message += " " + current.Message;
            }

            return message;
        }
    }
}
