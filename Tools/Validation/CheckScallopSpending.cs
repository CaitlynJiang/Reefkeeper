using System;
using System.Reflection;
using UnityEngine;
using Reefkeeper;

public static class CheckScallopSpending
{
 static void Need(bool valid,string message){if(!valid)throw new Exception(message);}
 public static string Main()
 {
  Need(Application.isPlaying,"Enter underwater Play mode first.");
  var wallet=UnityEngine.Object.FindFirstObjectByType<ReefScallopReward>();
  Need(wallet,"Reward wallet not installed.");
  var advance=typeof(ReefScallopReward).GetMethod("Advance",BindingFlags.NonPublic|BindingFlags.Instance);
  var point=Camera.main.transform.position+Camera.main.transform.forward;
  int paid=0,arrivals=0;float volume=wallet.volume;wallet.volume=0;
  Action<int> spent=n=>paid+=n;Action<int> collected=n=>arrivals++;
  wallet.Spent+=spent;wallet.Collected+=collected;
  try
  {
   wallet.ResetSession();
   Need(!wallet.TrySpend(1,point)&&wallet.Balance==0&&paid==0,"Insufficient funds changed the balance.");
   wallet.Award(3,point);
   Need(wallet.TrySpend(3,point)&&wallet.Balance==0&&paid==3,"Immediate purchase did not debit exactly once.");
   Need(!wallet.TrySpend(1,point)&&paid==3,"Pending animation allowed overspending.");
   for(int i=0;i<120;i++)
   {
    advance.Invoke(wallet,new object[]{.05f});
    Need(wallet.DisplayedBalance>=0,"Purchase animation made visible currency negative before earned shells arrived.");
   }
   Need(wallet.DisplayedBalance==0&&wallet.Balance==0&&arrivals==3&&wallet.InFlight==0&&!wallet.Visible,
       "Combined collection/payment did not settle and fade.");
   wallet.ShowShortfall();
   for(int i=0;i<80;i++)advance.Invoke(wallet,new object[]{.05f});
   Need(wallet.Balance==0&&!wallet.Visible,"Shortfall feedback changed currency or did not fade.");
   return "PASS: insufficient funds, spending during incoming rewards, no negative visual balance, reverse world flights, exact debit/event totals and feedback fade.";
  }
  finally {wallet.Spent-=spent;wallet.Collected-=collected;wallet.ResetSession();wallet.volume=volume;}
 }
}
