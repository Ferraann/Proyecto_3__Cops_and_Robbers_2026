using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    // GameObjects
    public GameObject board;
    public GameObject[] cops = new GameObject[2];
    public GameObject robber;
    public Text rounds;
    public Text finalMessage;
    public Button playAgainButton;

    // Otras variables
    Tile[] tiles = new Tile[Constants.NumTiles];
    private int roundCount = 0;
    private int state;
    private int clickedTile = -1;
    private int clickedCop = 0;

    void Start()
    {
        InitTiles();
        InitAdjacencyLists();
        state = Constants.Init;
    }

    // Rellenamos el array de casillas y posicionamos las fichas
    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;

            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();
            }
        }

        cops[0].GetComponent<CopMove>().currentTile = Constants.InitialCop0;
        cops[1].GetComponent<CopMove>().currentTile = Constants.InitialCop1;
        robber.GetComponent<RobberMove>().currentTile = Constants.InitialRobber;
    }

    // -------------------------------------------------------------------------
    // PASO 1: Listas de Adyacencia (Grafo No Dirigido)[cite: 1, 2]
    // -------------------------------------------------------------------------
    public void InitAdjacencyLists()
    {
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            int row = i / Constants.TilesPerRow;
            int col = i % Constants.TilesPerRow;

            // Vecino ARRIBA
            if (row > 0) tiles[i].adjacency.Add(i - Constants.TilesPerRow);
            // Vecino ABAJO
            if (row < Constants.TilesPerRow - 1) tiles[i].adjacency.Add(i + Constants.TilesPerRow);
            // Vecino IZQUIERDA
            if (col > 0) tiles[i].adjacency.Add(i - 1);
            // Vecino DERECHA
            if (col < Constants.TilesPerRow - 1) tiles[i].adjacency.Add(i + 1);
        }
    }

    // -------------------------------------------------------------------------
    // PASO 2: Algoritmo BFS para encontrar casillas seleccionables[cite: 1, 2]
    // -------------------------------------------------------------------------
    public void FindSelectableTiles(bool cop)
    {
        ResetTiles();

        int indexcurrentTile;
        if (cop)
            indexcurrentTile = cops[clickedCop].GetComponent<CopMove>().currentTile;
        else
            indexcurrentTile = robber.GetComponent<RobberMove>().currentTile;

        Queue<Tile> nodes = new Queue<Tile>();
        Tile startTile = tiles[indexcurrentTile];
        startTile.visited = true;
        startTile.distance = 0;
        nodes.Enqueue(startTile);

        while (nodes.Count > 0)
        {
            Tile t = nodes.Dequeue();

            if (t.distance < 2)
            {
                foreach (int neighborIndex in t.adjacency)
                {
                    Tile neighbor = tiles[neighborIndex];

                    // Bloqueo: No saltar sobre el otro policía
                    bool isOtherCopHere = false;
                    if (cop)
                    {
                        int otherCopId = (clickedCop == 0) ? 1 : 0;
                        if (neighborIndex == cops[otherCopId].GetComponent<CopMove>().currentTile)
                            isOtherCopHere = true;
                    }

                    if (!neighbor.visited && !isOtherCopHere)
                    {
                        neighbor.visited = true;
                        neighbor.parent = t;
                        neighbor.distance = t.distance + 1;
                        neighbor.selectable = true;
                        nodes.Enqueue(neighbor);
                    }
                }
            }
        }
        tiles[indexcurrentTile].current = true;
    }

    // -------------------------------------------------------------------------
    // PASO 3: Turno del Ladrón (IA Inteligente: Maximizar distancia)[cite: 2]
    // -------------------------------------------------------------------------
    public void RobberTurn()
    {
        // 1. Obtenemos las casillas alcanzables por el ladrón (máximo 2 saltos)[cite: 1]
        FindSelectableTiles(false);

        List<Tile> selectableTiles = new List<Tile>();
        foreach (Tile t in tiles)
        {
            if (t.selectable) selectableTiles.Add(t);
        }

        if (selectableTiles.Count > 0)
        {
            // 2. Obtenemos la posición actual de ambos policías
            int cop0Tile = cops[0].GetComponent<CopMove>().currentTile;
            int cop1Tile = cops[1].GetComponent<CopMove>().currentTile;

            // 3. Calculamos la distancia desde cada policía a todas las casillas del tablero[cite: 2]
            int[] distFromCop0 = CalculateDistancesBFS(cop0Tile);
            int[] distFromCop1 = CalculateDistancesBFS(cop1Tile);

            Tile bestTile = null;
            int maxMinDistance = -1; // Guardará la mejor (mayor) distancia encontrada

            // 4. Evaluamos cada casilla a la que puede saltar el ladrón
            foreach (Tile candidate in selectableTiles)
            {
                // Para esta casilla candidata, ¿cuál es la distancia al policía que tiene más cerca?[cite: 2]
                int distToNearestCop = Mathf.Min(distFromCop0[candidate.numTile], distFromCop1[candidate.numTile]);

                // Si esta casilla le ofrece una distancia segura mayor que la mejor que teníamos, la actualizamos[cite: 2]
                if (distToNearestCop > maxMinDistance)
                {
                    maxMinDistance = distToNearestCop;
                    bestTile = candidate;
                }
            }

            // 5. Movemos el ladrón a la mejor casilla encontrada[cite: 2]
            if (bestTile != null)
            {
                robber.GetComponent<RobberMove>().MoveToTile(bestTile);
                robber.GetComponent<RobberMove>().currentTile = bestTile.numTile;
            }
        }
    }

    // -------------------------------------------------------------------------
    // PASO 4: Gestión de colores y fin de juego
    // -------------------------------------------------------------------------
    public void EndGame(bool end)
    {
        if (end)
        {
            finalMessage.text = "You Win!";
            finalMessage.color = Color.green; // Texto en verde para victoria
        }
        else
        {
            finalMessage.text = "You Lose!";
            finalMessage.color = Color.red;   // Texto en rojo para derrota
        }
        playAgainButton.interactable = true;
        state = Constants.End;
    }

    // Solución al problema del botón Play Again
    public void PlayAgain()
    {
        // 1. Devolvemos las fichas a sus posiciones de Constants
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);

        // 2. Limpiamos colores y tablero
        ResetTiles();

        // 3. Reseteamos UI
        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount = 0;
        rounds.text = "Rounds: 0";

        // 4. IMPORTANTE: Cambiamos el estado para que el juego vuelva a empezar
        state = Constants.Init;
    }

    // --- Funciones de apoyo ---

    public void ResetTiles()
    {
        foreach (Tile tile in tiles) tile.Reset();
    }

    public void ClickOnCop(int cop_id)
    {
        if (state == Constants.Init || state == Constants.CopSelected)
        {
            clickedCop = cop_id;
            clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;
            FindSelectableTiles(true);
            state = Constants.CopSelected;
        }
    }

    public void ClickOnTile(int t)
    {
        if (state == Constants.CopSelected && tiles[t].selectable)
        {
            clickedTile = t;
            cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
            cops[clickedCop].GetComponent<CopMove>().currentTile = tiles[clickedTile].numTile;
            state = Constants.TileSelected;
        }
    }

    public void FinishTurn()
    {
        if (state == Constants.TileSelected)
        {
            ResetTiles();
            state = Constants.RobberTurn;
            RobberTurn();
        }
        else if (state == Constants.RobberTurn)
        {
            ResetTiles();
            IncreaseRoundCount();
            if (roundCount < Constants.MaxRounds)
                state = Constants.Init;
            else
                EndGame(false);
        }
    }

    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }

    public void InitGame() { state = Constants.Init; }

    // -------------------------------------------------------------------------
    // NUEVO: BFS Global para medir distancias reales (Ladrón Inteligente)
    // -------------------------------------------------------------------------
    public int[] CalculateDistancesBFS(int startNodeIndex)
    {
        // Creamos un array para guardar las distancias a cada casilla
        int[] distances = new int[Constants.NumTiles];

        // Inicializamos todas las distancias a -1 (indicando que no han sido visitadas)
        for (int i = 0; i < distances.Length; i++)
        {
            distances[i] = -1;
        }

        Queue<int> nodes = new Queue<int>();

        // El nodo de inicio tiene distancia 0
        nodes.Enqueue(startNodeIndex);
        distances[startNodeIndex] = 0;

        while (nodes.Count > 0)
        {
            int curr = nodes.Dequeue();

            // Exploramos todos los vecinos del nodo actual
            foreach (int neighborIndex in tiles[curr].adjacency)
            {
                // Si el vecino no ha sido visitado aún
                if (distances[neighborIndex] == -1)
                {
                    // Su distancia es la del nodo actual + 1
                    distances[neighborIndex] = distances[curr] + 1;
                    nodes.Enqueue(neighborIndex);
                }
            }
        }

        return distances;
    }
}