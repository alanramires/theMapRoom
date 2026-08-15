using System.Collections.Generic;

/// <summary>
/// O que bloco, campanha e quadrante tem em comum: sao RETANGULOS do mapa de
/// autoria, aninhados, com um portao de destrave.
///
///   MUNDO          uma cena de autoria, um asset
///    └─ BLOCO          Europeu, America do Norte, Russia
///        └─ CAMPANHA       Europa, Africa
///            └─ QUADRANTE      Inglaterra, Franca, Argelia, Egito
///
/// A interface existe pra ferramenta desenhar UM renderizador de nivel em vez de
/// tres quase iguais. Retangulo, destrave e continencia sao a mesma coisa em toda
/// escala; o que muda e so quem esta embaixo.
/// </summary>
public interface INoDoMapa
{
    string Id { get; set; }
    string Nome { get; set; }

    int OriginX { get; set; }
    int OriginY { get; set; }
    int Width { get; set; }
    int Height { get; set; }

    /// <summary>
    /// Ids de nos que precisam estar CONCLUIDOS para este destravar. Vazio = livre.
    ///
    /// "Concluido" e recursivo, e e isso que faz um campo so resolver os tres
    /// niveis:
    ///   quadrante concluido = venci ele
    ///   campanha  concluida = todos os quadrantes dela concluidos
    ///   bloco     concluido = todas as campanhas dele concluidas
    ///
    /// Entao "complete as campanhas do bloco X" se escreve DestravadoPor = ["X"].
    /// </summary>
    List<string> DestravadoPor { get; }

    /// <summary>
    /// O caso "last map": exige todos os IRMAOS concluidos. E flag e nao lista
    /// porque listar na mao quebra em silencio no dia em que se acrescenta um irmao.
    /// </summary>
    bool ExigeIrmaos { get; set; }
}
