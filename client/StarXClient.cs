﻿using SimpleJson;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using cocosocket4unity;

namespace StarX
{
  /// <summary>
  /// network state enum
  /// </summary>
  public enum NetWorkState
  {
    [Description("initial state")]
    CLOSED,

    [Description("connecting server")]
    CONNECTING,

    [Description("server connected")]
    CONNECTED,

    [Description("disconnected with server")]
    DISCONNECTED,

    [Description("connect timeout")]
    TIMEOUT,

    [Description("netwrok error")]
    ERROR
  }

  public class StarXClient : IDisposable
  {
    /// <summary>
    /// netwrok changed event
    /// </summary>
    public event Action<NetWorkState> NetWorkStateChangedEvent;


    private NetWorkState netWorkState = NetWorkState.CLOSED;   //current network state

    private EventManager eventManager;
    private MyKcp myKcpClient;
    private Protocol protocol;
    private bool disposed = false;
    private uint reqId = 1;

    private ManualResetEvent timeoutEvent = new ManualResetEvent(false);
    private int timeoutMSec = 8000;    //Connect timeout count in millisecond

    private string serverAddr = "42.192.128.111";

    private int port = 10187;

    public StarXClient()
    {
    }

    /// <summary>
    /// initialize pomelo client
    /// </summary>
    /// <param name="host">server name or server ip (www.xxx.com/127.0.0.1/::1/localhost etc.)</param>
    /// <param name="port">server port</param>
    /// <param name="callback">socket successfully connected callback(in network thread)</param>
    public void InitKcp(string host, int port, Action callback = null)
    {
      this.serverAddr = host;
      this.port = port;

      timeoutEvent.Reset();
      eventManager = new EventManager();
      NetWorkChanged(NetWorkState.CONNECTING);

      // kcp client 
      if (null != myKcpClient)
      {
        myKcpClient.Stop();
      }

      myKcpClient = new MyKcp();
      myKcpClient.NoDelay(1, 10, 2, 1);//fast
      myKcpClient.WndSize(4096, 4096);
      myKcpClient.Timeout(40 * 1000);
      myKcpClient.SetMtu(1400);
      myKcpClient.SetMinRto(10);
      myKcpClient.SetConv(121106);

      myKcpClient.Connect(this.serverAddr, this.port);
      myKcpClient.Start();

      Debug.Log("client 初始化成功" + this.serverAddr + ":" + this.port);

      try
      {
        this.protocol = new Protocol(this, myKcpClient);
        NetWorkChanged(NetWorkState.CONNECTED);

        if (callback != null)
        {
          callback();
        }
      }
      catch (SocketException e)
      {
        Debug.Log("client 初始化失败" + e);
        if (netWorkState != NetWorkState.TIMEOUT)
        {
          NetWorkChanged(NetWorkState.ERROR);
        }
        Dispose();
      }
      finally
      {
        timeoutEvent.Set();
      }
      // }
      // };


      if (timeoutEvent.WaitOne(timeoutMSec, false))
      {
        if (netWorkState != NetWorkState.CONNECTED && netWorkState != NetWorkState.ERROR)
        {
          NetWorkChanged(NetWorkState.TIMEOUT);
          Dispose();
        }
      }
    }

    /// <summary>
    /// initialize pomelo client
    /// </summary>
    /// <param name="host">server name or server ip (www.xxx.com/127.0.0.1/::1/localhost etc.)</param>
    /// <param name="port">server port</param>
    /// <param name="callback">socket successfully connected callback(in network thread)</param>
    // public void Init(string host, int port, Action callback = null)
    // {
    //   timeoutEvent.Reset();
    //   eventManager = new EventManager();
    //   NetWorkChanged(NetWorkState.CONNECTING);
    //   this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    //   socket.BeginConnect(host, port, new AsyncCallback((result) =>
    //   {
    //     try
    //     {
    //       this.socket.EndConnect(result);
    //       this.protocol = new Protocol(this, this.socket);
    //       NetWorkChanged(NetWorkState.CONNECTED);

    //       if (callback != null)
    //       {
    //         callback();
    //       }
    //     }
    //     catch (SocketException e)
    //     {
    //       if (netWorkState != NetWorkState.TIMEOUT)
    //       {
    //         NetWorkChanged(NetWorkState.ERROR);
    //       }
    //       Dispose();
    //     }
    //     finally
    //     {
    //       timeoutEvent.Set();
    //     }
    //   }), this.socket);

    //   if (timeoutEvent.WaitOne(timeoutMSec, false))
    //   {
    //     if (netWorkState != NetWorkState.CONNECTED && netWorkState != NetWorkState.ERROR)
    //     {
    //       NetWorkChanged(NetWorkState.TIMEOUT);
    //       Dispose();
    //     }
    //   }
    // }

    /// <summary>
    /// 网络状态变化
    /// </summary>
    /// <param name="state"></param>
    private void NetWorkChanged(NetWorkState state)
    {
      netWorkState = state;

      if (NetWorkStateChangedEvent != null)
      {
        NetWorkStateChangedEvent(state);
      }
    }

    public bool Connect(Action<byte[]> handshakeCallback)
    {
      try
      {
        protocol.start(handshakeCallback);
        return true;
      }
      catch (Exception e)
      {
#if UNITY_EDITOR
                Debug.Log(e.ToString());
#endif
        return false;
      }
    }

    public void Request(string route, byte[] body, Action<byte[]> action)
    {
      this.eventManager.AddCallBack(reqId, action);
      protocol.send(route, reqId, body);
      reqId++;
    }

    public void Notify(string route, byte[] body)
    {
      protocol.send(route, body);
    }

    public void On(string eventName, Action<byte[]> action)
    {
      eventManager.AddOnEvent(eventName, action);
    }

    internal void processMessage(Message msg)
    {
      if (msg.type == MessageType.MSG_RESPONSE)
      {
        eventManager.InvokeCallBack(msg.id, msg.data);
      }
      else if (msg.type == MessageType.MSG_PUSH)
      {
        eventManager.InvokeOnEvent(msg.route, msg.data);
      }
    }

    public void Disconnect()
    {
      Dispose();
      NetWorkChanged(NetWorkState.DISCONNECTED);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    // The bulk of the clean-up code
    protected virtual void Dispose(bool disposing)
    {
      if (this.disposed)
        return;

      if (disposing)
      {
        // free managed resources
        if (this.protocol != null)
        {
          this.protocol.close();
        }

        if (this.eventManager != null)
        {
          this.eventManager.Dispose();
        }

        try
        {
          this.myKcpClient.Stop();
          this.myKcpClient = null;
        }
        catch (Exception)
        {
          //todo : 有待确定这里是否会出现异常，这里是参考之前官方github上pull Request。emptyMsg
        }

        this.disposed = true;
      }
    }
  }
}