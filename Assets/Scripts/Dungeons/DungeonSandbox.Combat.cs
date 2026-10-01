using System.Collections.Generic;
using MiniWar.Online;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Dungeons
{
    public sealed partial class DungeonSandbox
    {
        readonly Dictionary<string,LanCombatRoom> combatRooms=new Dictionary<string,LanCombatRoom>();
        readonly CombatFighter localFighter=new CombatFighter{Id="P1"};
        LanCombatRoom currentCombat;
        DungeonCombatView combatView;
        OnlineVisuals combatVisuals;
        bool localFire,localAiming,localReviveUsed;
        float localAim;
        int localWeapon=3;
        bool LocalDead=>localFighter.Health<=0;
        void InitializeCombatView()
        {
            string key=RoomRun==null?"single":RoomRun.Current.id;
            foreach(var previous in combatRooms.Values)previous.Pause();
            if(!combatRooms.TryGetValue(key,out currentCombat))combatRooms[key]=currentCombat=new LanCombatRoom(DungeonCombatData.Room(layout));
            if(RoomRun!=null&&RoomRun.Cleared)currentCombat.ClearForPreview();
            combatView?.Dispose();combatView=new DungeonCombatView(worldRoot,square);
            localFighter.ProtectedUntil=Time.time+1;
        }
        void ReadCombatInput()
        {
            var kb=Keyboard.current;var mouse=Mouse.current;
            if(Application.isFocused&&kb!=null)
            {
                int weapon=kb.digit1Key.wasPressedThisFrame?3:kb.digit2Key.wasPressedThisFrame?5:localWeapon;
                if(weapon!=localWeapon){localWeapon=weapon;rig.SetWeapon(combatVisuals.Weapon(weapon,1),OnlineVisuals.WeaponWidths[weapon]);}
            }
            localFire=Application.isFocused&&mouse!=null&&mouse.leftButton.isPressed&&!LocalDead&&!WaitingInPortal;
            localAiming=!LocalDead&&mouse!=null&&(mouse.rightButton.isPressed||localFire);
            if(localAiming)
            {
                var aim=(Vector2)view.ScreenToWorldPoint(mouse.position.ReadValue())-(Motor.Position+Vector2.up*LanShooting.AimHeight);
                localAim=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            }
        }
        void TickLocalCombat()
        {
            if(currentCombat==null)return;
            localFighter.X=Motor.Position.x;localFighter.Y=Motor.Position.y;localFighter.Active=!LocalDead&&!WaitingInPortal;
            int facing=localAiming?LanAim.Facing(localAim,Motor.Facing):Motor.Facing;
            float power=party.Count>0&&party.Count<=4?LanRules.DungeonPower(party.Count):1;
            if(localFire&&currentCombat.Attack(localFighter,localWeapon,1,0,localAim,facing,power,Time.time))rig.Recoil();
            currentCombat.Tick(Time.fixedDeltaTime,new[]{localFighter},Time.time);
            if(RoomRun!=null)
            {
                party[0].spectator=LocalDead;
                foreach(var enemy in currentCombat.Enemies)if(enemy.hp<=0)
                {int split=enemy.id.LastIndexOf(':');RoomRun.Defeat(enemy.id.Substring(0,split),int.Parse(enemy.id.Substring(split+1)));}
            }
            combatView.Refresh(currentCombat.Enemies,currentCombat.Projectiles);
        }
        void ResetOrRevive()
        {
            if(LocalDead)
            {
                if(localReviveUsed)return;
                localReviveUsed=true;localFighter.Health=100;localFighter.ProtectedUntil=Time.time+2;
                if(party.Count>0)party[0].spectator=false;
            }
            Motor.Reset();jump=dash=drop=false;
        }
        void DrawLocalCombatHUD()
        {
            GUI.Box(new Rect(14,137,550,70),GUIContent.none);
            GUI.Label(new Rect(24,141,530,26),$"HP {Mathf.CeilToInt(localFighter.Health)} / 100 · 1 소총 / 2 근접 · 좌클릭 공격",label);
            GUI.Label(new Rect(24,172,530,25),LocalDead?(localReviveUsed?"부활 기회 소진 · 관전 중":"R 부활 · 던전당 한 번"):
                "주황 전조: 근접·석궁 / 보라 전조: 마법 · 탄약 소모·보상 미적용",label);
        }
        void DrawLivingEnemyNames()
        {
            if(currentCombat==null)return;
            foreach(var state in currentCombat.Enemies)
            {
                if(state.hp<=0)continue;
                int split=state.id.LastIndexOf(':');
                var spawn=layout.spawns.Find(s=>s.id==state.id.Substring(0,split));
                if(spawn!=null)DrawLabel(new Vector2(state.x,state.y+state.height+.65f),spawn.Label);
            }
        }
    }
}
