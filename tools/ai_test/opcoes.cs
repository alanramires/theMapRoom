using System.Reflection;
// Para cada unidade do slot: que predios ela alcanca neste turno e se o sensor deixa capturar,
// com o MOTIVO quando nao deixa (ex.: "Terreno ainda desconhecido" = predio no escuro).
// E aqui que se separa "a IA errou" de "a regra nao permite".
int meuSlot = 1;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var sb = new System.Text.StringBuilder();
var predios = UnityEngine.Object.FindObjectsByType<ConstructionManager>();
foreach (var u in UnitManager.AllActive)
{
    if (u == null || u.IsDead || u.SlotIndex != meuSlot) continue;
    var paths = UnitMovementPathRules.CalcularCaminhosValidos(u.BoardTilemap, u, u.RemainingMovementPoints, mc.TerrainDatabaseRef);
    var c = u.CurrentCellPosition;
    sb.AppendLine(u.name + " @" + c.x + "," + c.y + " mp=" + u.RemainingMovementPoints);
    foreach (var k in predios)
    {
        if (k.SlotIndex == meuSlot) continue;
        var kc = k.CurrentCellPosition; kc.z = 0;
        bool parado = kc.x == c.x && kc.y == c.y;
        if (!parado && !paths.ContainsKey(kc)) continue;
        bool pode = PodeCapturarSensor.TryGetCaptureTargetAtCell(u, u.BoardTilemap, kc,
            parado ? SensorMovementMode.MoveuParado : SensorMovementMode.MoveuAndando,
            out ConstructionManager alvo, out PodeCapturarSensor.CaptureOperationType op, out string motivo, mc);
        sb.AppendLine("   alcanca " + k.name + " @" + kc.x + "," + kc.y + " captura=" + pode + (pode ? "" : " (" + motivo + ")"));
    }
}
return sb.ToString();
