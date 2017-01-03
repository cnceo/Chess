﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Networking;
using ZG.Network.Lobby;

public class MahjongClient : Client
{
    private Dictionary<byte, byte> __tiles;

    public Mahjong.Tile GetTile(byte code)
    {
        if(__tiles == null)
            return new Mahjong.Tile(Mahjong.TileType.Unknown, 0);

        byte tile;
        if (__tiles.TryGetValue(code, out tile))
            return tile;

        return new Mahjong.Tile(Mahjong.TileType.Unknown, 0);
    }

    public new void Create()
    {
        base.Create();

        RegisterHandler((short)MahjongNetworkMessageType.Shuffle, __OnShuffle);
        RegisterHandler((short)MahjongNetworkMessageType.TileCodes, __OnTileCodes);
        RegisterHandler((short)MahjongNetworkMessageType.RuleNodes, __OnRuleNodes);
    }

    private void __OnShuffle(NetworkMessage message)
    {
        MahjongShuffleMessage shuffleMessage = message == null ? null : message.ReadMessage<MahjongShuffleMessage>();
        if (shuffleMessage == null)
            return;


    }

    private void __OnTileCodes(NetworkMessage message)
    {
        MahjongTileCodeMessage tileCodeMessage = message == null ? null : message.ReadMessage<MahjongTileCodeMessage>();
        if (tileCodeMessage == null)
            return;

        if (__tiles != null)
            __tiles.Clear();

        if (tileCodeMessage.count > 0)
        {
            if(tileCodeMessage.tileCodes != null)
            {
                byte index = 0;
                foreach (byte tileCode in tileCodeMessage.tileCodes)
                {
                    if (__tiles == null)
                        __tiles = new Dictionary<byte, byte>();

                    __tiles.Add(tileCode, index++);
                }
            }
        }
    }

    private void __OnRuleNodes(NetworkMessage message)
    {
        MahjongRuleMessage ruleMessage = message == null ? null : message.ReadMessage<MahjongRuleMessage>();
        if (ruleMessage == null || ruleMessage.ruleNodes == null)
            return;
        
        MahjongClientRoom room = MahjongClientRoom.instance;
        if (room == null)
            return;

        int index = 0;
        List<int> chowIndices = null, pongIndices = null, kongIndices = null, winIndices = null;
        foreach(Mahjong.RuleNode ruleNode in ruleMessage.ruleNodes)
        {
            switch(ruleNode.type)
            {
                case Mahjong.RuleType.Chow:
                    if (chowIndices == null)
                        chowIndices = new List<int>();

                    chowIndices.Add(index);
                    break;
                case Mahjong.RuleType.Pong:
                    if (pongIndices == null)
                        pongIndices = new List<int>();

                    pongIndices.Add(index);
                    break;
                case Mahjong.RuleType.Kong:
                case Mahjong.RuleType.HiddenKong:
                case Mahjong.RuleType.MeldedKong:
                    if (kongIndices == null)
                        kongIndices = new List<int>();

                    kongIndices.Add(index);
                    break;
                case Mahjong.RuleType.Win:
                    if (winIndices == null)
                        winIndices = new List<int>();

                    winIndices.Add(index);
                    break;
            }

            ++index;
        }

        bool isSetEvent = false;
        if(chowIndices != null)
            isSetEvent = __SetEvent(ruleMessage.ruleNodes, chowIndices.AsReadOnly(), room.chow) || isSetEvent;

        if (pongIndices != null)
            isSetEvent = __SetEvent(ruleMessage.ruleNodes, pongIndices.AsReadOnly(), room.pong) || isSetEvent;

        if (kongIndices != null)
            isSetEvent = __SetEvent(ruleMessage.ruleNodes, kongIndices.AsReadOnly(), room.kong) || isSetEvent;

        if (winIndices != null)
            isSetEvent = __SetEvent(ruleMessage.ruleNodes, winIndices.AsReadOnly(), room.win) || isSetEvent;

        if (isSetEvent)
            Invoke("__ClearEvents", 5.0f);
    }

    private bool __SetEvent(ReadOnlyCollection<Mahjong.RuleNode> ruleNodes, ReadOnlyCollection<int> ruleIndices, Button button)
    {
        if (button == null)
            return false;

        int numRules = ruleIndices == null ? 0 : ruleIndices.Count;
        if (numRules < 1)
            return false;

        GameObject gameObject = button.gameObject;
        if (gameObject != null)
            gameObject.SetActive(true);

        Button.ButtonClickedEvent buttonClickedEvent = new Button.ButtonClickedEvent();
        button.onClick = buttonClickedEvent;

        UnityAction listener = null;
        listener = delegate()
        {
            MahjongClientPlayer player = localPlayer as MahjongClientPlayer;
            if (player == null)
                return;

            if (numRules < 2)
            {
                player.Try((byte)ruleIndices[0]);

                __ClearEvents();
            }
            else
            {
                foreach (int ruleIndex in ruleIndices)
                {
                    int temp = ruleIndex;
                    player.Select(ruleNodes[temp], delegate ()
                    {
                        player.Try((byte)temp);

                        __ClearEvents();
                    });
                }
            }
        };

        buttonClickedEvent.AddListener(listener);

        return true;
    }

    private void __ClearEvents()
    {
        CancelInvoke("__ClearEvents");

        MahjongClientRoom room = MahjongClientRoom.instance;
        if (room == null)
            return;

        MahjongClientPlayer player = localPlayer as MahjongClientPlayer;
        if (player != null)
            player.Unselect();

        if (room.chow != null)
        {
            room.chow.onClick = null;

            GameObject gameObject = room.chow.gameObject;
            if (gameObject != null)
                gameObject.SetActive(false);
        }

        if (room.pong != null)
        {
            room.pong.onClick = null;

            GameObject gameObject = room.pong.gameObject;
            if (gameObject != null)
                gameObject.SetActive(false);
        }

        if (room.kong != null)
        {
            room.kong.onClick = null;

            GameObject gameObject = room.kong.gameObject;
            if (gameObject != null)
                gameObject.SetActive(false);
        }

        if (room.win != null)
        {
            room.win.onClick = null;

            GameObject gameObject = room.win.gameObject;
            if (gameObject != null)
                gameObject.SetActive(false);
        }
    }
}
