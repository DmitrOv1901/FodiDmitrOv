#nullable enable

using System.Collections.Generic;
using MinesServer.Data;
using UnityEngine;

namespace Kern.Networking.Buildings;
public abstract class PackBuilding
{
    public abstract BuildingType Type { get; }

    /// <summary>
    /// Сдвиг спрайта-надстройки от центра пак-точки в клетках: авторское
    /// выравнивание «крыши» по footprint, у каждого пака своё.
    /// </summary>
    public abstract Vector2 RoofCenterOffsetCells { get; }

    public abstract IEnumerable<((int X, int Y) Pos, CellType Cell)> CellsToPlace();
}
