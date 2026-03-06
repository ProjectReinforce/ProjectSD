using System;
using System.Collections.Generic;
using Shared.Kernel;

namespace Features.Lobby.Domain
{
    public sealed class Lobby
    {
        private readonly List<Room> _rooms = new List<Room>();

        public IReadOnlyList<Room> Rooms
        {
            get { return _rooms; }
        }

        public Result AddRoom(Room room)
        {
            if (room == null)
            {
                return Result.Failure("Room is required.");
            }

            if (FindRoom(room.Id) != null)
            {
                return Result.Failure("Room id already exists.");
            }

            if (FindRoomByName(room.Name) != null)
            {
                return Result.Failure("Room name already exists.");
            }

            _rooms.Add(room);
            return Result.Success();
        }

        public Result RemoveRoom(EntityId roomId)
        {
            var room = FindRoom(roomId);
            if (room == null)
            {
                return Result.Failure("Room was not found.");
            }

            _rooms.Remove(room);
            return Result.Success();
        }

        public Room FindRoom(EntityId roomId)
        {
            return _rooms.Find(room => room.Id.Equals(roomId));
        }

        public Room FindRoomByName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return null;
            }

            var target = roomName.Trim();
            return _rooms.Find(room => string.Equals(room.Name, target, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class DomainRule
    {
        public static Result ValidateRoomName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return Result.Failure("Room name is required.");
            }

            var normalized = roomName.Trim();
            if (normalized.Length < 2)
            {
                return Result.Failure("Room name must be at least 2 characters.");
            }

            return Result.Success();
        }

        public static Result EnsureUniqueRoomName(Lobby lobby, string roomName)
        {
            if (lobby == null)
            {
                return Result.Failure("Lobby is required.");
            }

            if (lobby.FindRoomByName(roomName) != null)
            {
                return Result.Failure("Room name already exists.");
            }

            return Result.Success();
        }

        public static Result CanStartGame(Room room)
        {
            if (room == null)
            {
                return Result.Failure("Room is required.");
            }

            if (!room.CanStartGame())
            {
                return Result.Failure("All players must be ready and room must have at least two members.");
            }

            return Result.Success();
        }
    }
}
