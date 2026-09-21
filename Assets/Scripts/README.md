# 무기전쟁 — 코어 스크립트

Unity 6 / C# 9 기준. 외부 패키지 의존 없음. `Assets/Scripts/` 아래 그대로 넣으면 된다.

## 파일 구성

```
Data/       WeaponData · EnemyData · SegmentData · UpgradeTable   ← 수치가 들어가는 그릇
Runtime/    WeaponInstance · Loadout · EconomySystem · RunState   ← 한 판 동안의 상태
Combat/     DamageCalculator · EnemyHealth · BalanceProbe         ← 계산과 검산
```

## 설계상 지켜야 할 세 가지

**1. 피해 계산은 `DamageCalculator` 한 곳에서만 한다.**
방어력은 펠릿마다 감산된다. 산탄형이 장갑형 앞에서 급격히 약해지고 관통(고위력형)이 답이 되는 게 여기서 나온다. 사격 코드에서 따로 계산하면 밸런싱 표와 게임이 어긋난다.

**2. 보상 배율은 체력 배율보다 낮아야 한다.**
`SegmentData.OnValidate`가 어기면 경고를 띄운다. 킬당 마진의 **부호**가 양수로 남으면 적이 많아질수록 부자가 되어 경제 압박이 사라진다. (1.0/1.5/2.2로 잡았다가 이 문제로 폐기한 이력이 있음)

**3. 파산은 전탄 0 + 잔액이 최저 장전비 미만일 때만이다.**
잔액 0만으로는 패배가 아니다. 남은 탄을 다 쓸 때까지는 살아 있다. 사격 직후와 장전 실패 시 `EconomySystem.CheckBankruptcy()`를 부른다.

## 에셋에 넣을 값 (수치표 1차안)

### WeaponData ×4
| displayName | slot | damagePerPellet | pellets | magazineSize | reloadCost | shotsPerSecond | piercing |
|---|---|---|---|---|---|---|---|
| 권총 | 1 | 25 | 1 | 12 | 9 | 2.0 | ☐ |
| 연사형 | 2 | 24 | 1 | 40 | 400 | 8.0 | ☐ |
| 산탄형 | 3 | 30 | 4 | 8 | 320 | 1.5 | ☐ |
| 고위력형 | 4 | 250 | 1 | 5 | 750 | 0.8 | ☑ |

### EnemyData ×4
| displayName | baseHealth | armor | baseReward | damagePerSecond | isBoss |
|---|---|---|---|---|---|
| 근접 돌진형 | 100 | 0 | 60 | 6 | ☐ |
| 원거리 사격형 | 80 | 0 | 70 | 4 | ☐ |
| 장갑형 | 300 | 20 | 330 | 10 | ☐ |
| 보스 | 1500 | 20 | — | 14 | ☑ |

### SegmentData ×3
| index | lengthMeters | healthMultiplier | rewardMultiplier | 적 구성 |
|---|---|---|---|---|
| 1 | 100 | 1.0 | 1.00 | 근접 6 · 원거리 3 · 장갑 1 |
| 2 | 150 | 1.6 | 1.15 | 근접 7 · 원거리 4 · 장갑 2 |
| 3 | 200 | 2.5 | 1.30 | 근접 8 · 원거리 5 · 장갑 3 |

### UpgradeTable ×1
damage 1.35 · fireRate 1.25 · magazine 1.30 · reloadCost 0.70 · upgradesPerRun 3

### 플레이어
`RunState(280f)` · `EconomySystem(loadout, startingMoney: 300, warningMultiple: 3f)` · 구간 클리어 시 체력 30 회복

## 검산 방법

빈 GameObject에 `BalanceProbe`를 붙이고 위 에셋들을 꽂은 뒤, 인스펙터 우클릭 → **킬당 마진 표 출력**.
기획서 07-4 표와 대조한다. 마지막 구간에서 비싼 총의 마진이 음수가 아니면 경제 압박이 성립하지 않은 것이다.

검증된 기대값 (강화 없음, 근접 돌진형 기준):

| 구간 | 권총 | 연사 | 산탄 | 장갑형·고위력 |
|---|---|---|---|---|
| 1 | +57 | +10 | +20 | +30 |
| 2 | +64 | −1 | −11 | +79 |
| 3 | +71 | −32 | −42 | −21 |

## 아직 없는 것

사격 입력, 조준, 적 이동·스폰, 강화 선택 UI, HUD. 이 스크립트들은 그 시스템들이 올라탈 **데이터와 규칙 계층**까지다.
다음 순서는 Day 2의 조준·사격 입력과 `EnemyHealth`를 붙이는 일.
