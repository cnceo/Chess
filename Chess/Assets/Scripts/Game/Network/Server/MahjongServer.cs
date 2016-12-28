﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;
using ZG;
using ZG.Network.Lobby;

public class MahjongServer : Server
{
    private struct Player
    {
        public string roomName;
        public int index;
    }

    private class Room
    {
        public class Player : Mahjong.Player
        {
            private static List<KeyValuePair<int, Mahjong.Tile>> __tiles;
             
            private byte[] __tileCodes;
            private MahjongServerPlayer __instance;

            public MahjongServerPlayer instance
            {
                get
                {
                    return __instance;
                }

                set
                {
                    __instance = value;

                    __Reset();
                }
            }

            public Player(Room room) : base(room.__mahjong)
            {

            }

            public void Reset()
            {
                int count = (int)Mahjong.TileType.Unknown, step = (256 - count) / count;
                if (__tileCodes == null)
                    __tileCodes = new byte[count];

                int index = 0;
                for (int i = 0; i < count; ++i)
                    __tileCodes[i] = (byte)UnityEngine.Random.Range(index, index += step);

                __Reset();
            }

            public void SendRuleMessage(ReadOnlyCollection<Mahjong.RuleNode> ruleNodes)
            {
                MahjongServer host = __instance == null ? null : __instance.host as MahjongServer;
                if (host != null)
                {
                    Node node;
                    if (host.GetNode(__instance.node.index, out node))
                        host.SendRuleMessage(node.connectionId, ruleNodes);
                }
            }
            
            public Mahjong.RuleType End(int index)
            {
                Action<int> handler = delegate (int temp)
                {
                    if (__tiles == null)
                        __tiles = new List<KeyValuePair<int, Mahjong.Tile>>();

                    __tiles.Add(new KeyValuePair<int, Mahjong.Tile>(temp, GetHandTile(temp)));
                };

                if (__tiles != null)
                    __tiles.Clear();

                Mahjong.RuleType type;
                byte group = (byte)base.End(index, handler, out type);
                if (__instance != null)
                {
                    foreach (KeyValuePair<int, Mahjong.Tile> pair in __tiles)
                        __instance.Throw((byte)pair.Key, group, pair.Value);

                    __instance.Do(type, group);
                }

                return type;
            }

            public bool Draw()
            {
                return Draw(__Add, __Remove);
            }

            public new bool Discard(int index)
            {
                Mahjong.Tile tile = GetHandTile(index);
                if(base.Discard(index))
                {
                    if (instance != null)
                        instance.Throw((byte)index, 255, tile);

                    return true;
                }

                return false;
            }

            public new bool Try(int index)
            {
                Mahjong.RuleType type = Get(index).type;
                if(base.Try(index))
                {
                    if (__instance != null)
                        __instance.Try(type);

                    return true;
                }

                return false;
            }

            public IEnumerator WaitToThrow(float timeout)
            {
                if (__instance == null)
                    return null;

                return __instance.Wait((byte)handleTileIndex, (short)MahjongNetworkRPCHandle.Throw, timeout, __OnThrow);
            }

            public IEnumerator WaitToTry(float timeout)
            {
                if (__instance == null)
                    return null;

                return __instance.Wait((byte)handleTileIndex, (short)MahjongNetworkRPCHandle.Try, timeout, __OnTry);
            }
            
            private void __Reset()
            {
                if (__tileCodes != null)
                {
                    MahjongServer host = __instance == null ? null : __instance.host as MahjongServer;
                    if (host != null)
                    {
                        Node node;
                        if (host.GetNode(__instance.node.index, out node))
                            host.SendTileCodeMessage(node.connectionId, (byte)__tileCodes.Length, __tileCodes);
                    }
                }
            }

            private void __Add(int index)
            {
                if (__tileCodes == null || instance == null)
                    return;
                
                byte code = GetHandTile(index);
                if (code >= __tileCodes.Length)
                    return;
                
                instance.Draw((byte)index, __tileCodes[code]);
            }

            private void __Remove(int index)
            {
                if (instance != null)
                    instance.Throw((byte)index, 255, GetHandTile(index));
            }
            
            private void __OnThrow(byte index)
            {
                Discard(index);
            }

            private void __OnTry(byte index)
            {
                Try(index);
            }

        }

        private bool __isRunning;
        private int __index;
        private int __point0;
        private int __point1;
        private int __point2;
        private int __point3;
        private Mahjong __mahjong;

        public bool isRunning
        {
            get
            {
                return __isRunning;
            }
        }

        public int index
        {
            get
            {
                return __index;
            }
        }

        public Room(int index)
        {
            __index = index;

            __mahjong = new Mahjong();
        }

        public Player Get(int index)
        {
            if (__mahjong == null)
                return null;

            return __mahjong.Get(index) as Player;
        }

        public IEnumerator Run()
        {
            __isRunning = true;

            if (__mahjong == null)
            {
                __mahjong = new Mahjong();

                __mahjong.rule = new Mahjong.Rule();
            }
            else if(__mahjong.rule == null)
                __mahjong.rule = new Mahjong.Rule();

            Player player;

            IEnumerable<Mahjong.Player> players = __mahjong.players;
            if(players != null)
            {
                foreach(Mahjong.Player temp in players)
                {
                    player = temp as Player;
                    if (player != null)
                        player.Reset();
                }
            }

            __mahjong.Shuffle(out __point0, out __point1, out __point2, out __point3);
            int i, index, ruleNodeIndex;
            Mahjong.RuleType ruleType;
            Mahjong.RuleNode ruleNode;
            ReadOnlyCollection<Mahjong.RuleNode> ruleNodes;
            while (__mahjong.tileCount < 144)
            {
                index = __mahjong.playerIndex;
                player = __mahjong.Get(index) as Player;
                if(player != null)
                {
                    while (true)
                    {
                        if (player.isDraw)
                        {
                            ruleNodes = player.Start();
                            if (ruleNodes != null && ruleNodes.Count > 0)
                            {
                                player.SendRuleMessage(ruleNodes);

                                yield return player.WaitToTry(5.0f);

                                ruleType = __mahjong.ruleType;
                                if (ruleType != Mahjong.RuleType.Unknown && index == __mahjong.rulePlayerIndex)
                                {
                                    ruleNodeIndex = __mahjong.ruleNodeIndex;
                                    ruleNode = player.Get(ruleNodeIndex);
                                    ruleType = ruleNode.type = player.End(ruleNodeIndex);

                                    if (ruleType == Mahjong.RuleType.Win)
                                    {
                                        __isRunning = false;

                                        __Break();

                                        yield break;
                                    }

                                    if (ruleType != Mahjong.RuleType.Unknown)
                                        continue;
                                }
                            }
                            
                            yield return player.WaitToThrow(6.0f);

                            for (i = 1; i < 4; ++i)
                            {
                                player = __mahjong.Get((i + index) & 3) as Player;
                                if (player != null)
                                {
                                    ruleNodes = player.Start();

                                    if (ruleNodes != null && ruleNodes.Count > 0)
                                    {
                                        player.SendRuleMessage(ruleNodes);

                                        yield return player.WaitToTry(5.0f);
                                    }
                                }
                            }

                            ruleType = __mahjong.ruleType;
                            if (ruleType == Mahjong.RuleType.Win)
                            {
                                __isRunning = false;

                                __Break();

                                yield break;
                            }

                            if (ruleType != Mahjong.RuleType.Unknown)
                            {
                                player = __mahjong.Get(__mahjong.rulePlayerIndex) as Player;
                                if (player != null)
                                {
                                    ruleNodeIndex = __mahjong.ruleNodeIndex;
                                    ruleNode = player.Get(ruleNodeIndex);
                                    ruleType = ruleNode.type = player.End(ruleNodeIndex);
                                }
                            }

                            break;
                        }
                        else if (!player.Draw())
                            break;
                    }
                }
                
                yield return new WaitForSeconds(2.0f);
            }
        }

        private void __Break()
        {
            int i, j, count;
            Node node;
            Player player;
            MahjongServerPlayer instance;
            ZG.Network.Server host;
            ZG.Network.Lobby.Node temp;
            for(i = 0; i < 4; ++i)
            {
                player = __mahjong.Get(index) as Player;
                instance = player == null ? null : player.instance;
                temp = instance == null ? null : instance.node as ZG.Network.Lobby.Node;
                if (temp != null)
                {
                    host = instance.host;
                    if (host != null && host.GetNode(temp.index, out node) && node.connectionId < 0)
                        host.Unregister(temp.index);
                    else
                    {
                        count = temp.count;
                        for (j = 0; j < count; ++j)
                            temp.NotReady();
                    }
                }
            }
        }
    }

    private Pool<string> __playerNames;
    private Pool<string> __roomNames;
    private Dictionary<string, Player> __playerMap;
    private Dictionary<string, Room> __roomMap;
    
    public int CreateRoom(string name)
    {
        if (__roomMap == null)
            __roomMap = new Dictionary<string, Room>();
        else if (__roomMap.ContainsKey(name))
            return -1;

        int roomIndex = nextRoomIndex;
        if (__roomNames == null)
            __roomNames = new Pool<string>();

        __roomNames.Insert(roomIndex, name);

        __roomMap[name] = new Room(roomIndex);

        return roomIndex;
    }

    public void SendTileCodeMessage(int connectionId, byte count, IEnumerable<byte> codes)
    {
        Send(connectionId, (short)MahjongNetworkMessageType.TileCodes, new MahjongTileCodeMessage(count, codes));
    }

    public void SendRuleMessage(int connectionId, ReadOnlyCollection<Mahjong.RuleNode> ruleNodes)
    {
        Send(connectionId, (short)MahjongNetworkMessageType.RuleNodes, new MahjongRuleMessage(ruleNodes));
    }
    
    public new void Create()
    {
        onRegistered += __OnRegistered;
        onReady += __OnReady;

        base.Create();

        RegisterHandler((short)MahjongNetworkMessageType.Room, __OnRoom);
    }
    
    protected override bool _GetRoomInfo(NetworkReader reader, int connectionId, int roomIndex, out int length)
    {
        length = 4;

        if (__roomNames == null || __roomMap == null)
            return false;

        string roomName;
        if (!__roomNames.TryGetValue(roomIndex, out roomName))
            return false;
        
        return __roomMap.ContainsKey(roomName);
    }

    protected override bool _GetPlayerInfo(NetworkReader reader, int connectionId, out short type, out int roomIndex, out int playerIndex)
    {
        type = 0;
        roomIndex = -1;
        playerIndex = -1;
        if (__roomMap == null)
            return false;

        InitMessage message = reader == null ? null : reader.ReadMessage<InitMessage>();
        if (message == null)
            return false;

        Room room;
        if (!__roomMap.TryGetValue(message.roomName, out room) || room == null)
            return false;

        if (__playerMap == null)
            __playerMap = new Dictionary<string, Player>();

        Player player;
        if (!__playerMap.TryGetValue(message.name, out player))
        {
            player.index = GetRoomNextIndex(roomIndex);
            player.roomName = message.roomName;
            __playerMap.Add(message.name, player);
            
            if (__playerNames == null)
                __playerNames = new Pool<string>();

            __playerNames.Insert(nextNodeIndex, message.name);
        }

        type = (short)(player.index >= 0 && player.index < 4 ? player.index : 4);
        roomIndex = room.index;
        playerIndex = player.index;

        return true;
    }

    protected override bool _Unregister(int index)
    {
        if (!base._Unregister(index))
            return false;

        if (__playerNames != null)
        {
            string name;
            if (__playerNames.TryGetValue(index, out name))
            {
                if (__playerMap != null)
                {
                    Player player;
                    if (__playerMap.TryGetValue(name, out player))
                    {
                        if (__roomMap != null)
                        {
                            Room room;
                            if (__roomMap.TryGetValue(player.roomName, out room) && room != null)
                            {
                                if (room.isRunning)
                                    return false;

                                Room.Player temp = room.Get(player.index);
                                if (temp != null)
                                    temp.index = -1;

                                if (GetCount(room.index) < 1)
                                {
                                    if (__roomNames != null)
                                        __roomNames.RemoveAt(room.index);

                                    __roomMap.Remove(player.roomName);
                                }
                            }
                        }

                        __playerMap.Remove(name);
                    }
                }

                __playerNames.RemoveAt(index);
            }
        }

        return true;
    }

    private void __OnRegistered(ZG.Network.Node node)
    {
        if (node == null || __playerNames == null || __playerMap == null)
            return;

        string playerName;
        if (!__playerNames.TryGetValue(node.index, out playerName))
            return;

        Player player;
        if (!__playerMap.TryGetValue(playerName, out player))
            return;

        if (player.index >= 0 && player.index < 4)
        {
            if (__roomMap == null)
                return;

            Room room;
            if (!__roomMap.TryGetValue(player.roomName, out room) || room == null)
                return;

            Room.Player temp = new Room.Player(room);
            temp.index = player.index;
            temp.instance = node == null ? null : node.GetComponent<MahjongServerPlayer>();
        }
    }

    private void __OnReady(int roomIndex, int count)
    {
        if (__roomNames == null || __roomMap == null)
            return;

        string roomName;
        if (!__roomNames.TryGetValue(roomIndex, out roomName))
            return;

        Room room;
        if (!__roomMap.TryGetValue(roomName, out room) || room == null)
            return;

        if(count == 1)
            StartCoroutine(room.Run());
    }

    private void __OnRoom(NetworkMessage message)
    {
        NetworkConnection connection = message == null ? null : message.conn;
        if (connection == null)
            return;

        string name = Guid.NewGuid().ToString();
        if (CreateRoom(name) == -1)
            return;

        Send(connection.connectionId, (short)MahjongNetworkMessageType.Room, new NameMessage(name));
    }
}
