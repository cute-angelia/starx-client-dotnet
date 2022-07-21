using System;
using System.Collections;
using System.Collections.Generic;
using cocosocket4unity;
using UnityEngine;
using StarX;

public class MyKcp : KcpClient
{

  private Transporter transporter;

  protected override void HandleReceive(ByteBuf bb)
  {
    // string content = System.Text.Encoding.UTF8.GetString(bb.GetRaw());
    // Console.WriteLine("msg:" + content);
    //this.Send(bb.Copy());
    this.transporter.receive(bb.GetRaw());
  }
  /// <summary>
  /// 异常
  /// </summary>
  /// <param name="ex"></param>
  protected override void HandleException(Exception ex)
  {
    base.HandleException(ex);
  }
  /// <summary>
  /// 超时
  /// </summary>
  protected override void HandleTimeout()
  {
    base.HandleTimeout();
  }

  public void setHandleReceive(Transporter transporter)
  {
    this.transporter = transporter;
  }
}