using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct CellInfo
{
    public int score;
    public short from_cell;
    public short to_cell;
}

public class PlayerAgent
{
    public CellInfo run(List<short> board, List<Player> players, List<short> attacker_cells, List<short> victim_cells)
    {
        List<CellInfo> cell_scores = new List<CellInfo>();
        int total_best_score = 0;
        attacker_cells.ForEach(cell =>
        {
            int best_score = 0;
            short cell_the_best = 0;
            List<short> available_cells = Helper.find_available_cells(cell, board, players);
            available_cells.ForEach(to_cell =>
            {
                int score = calc_score(cell, to_cell, victim_cells);
                if (best_score < score)
                {
                    cell_the_best = to_cell;
                    best_score = score;
                }
            });

            if (total_best_score < best_score)
            {
                total_best_score = best_score;
            }

            CellInfo info = new CellInfo();
            info.score = best_score;
            info.from_cell = cell;
            info.to_cell = cell_the_best;
            cell_scores.Add(info);
        });

        List<CellInfo> top_scores = cell_scores.FindAll(info => info.score == total_best_score);
        System.Random rnd = new System.Random();
        int index = rnd.Next(0, top_scores.Count);
        return top_scores[index];
    }

    int calc_score(short from_cell, short to_cell, List<short> victim_cells)
    {
        int score = 0;

        short distance = Helper.get_distance(from_cell, to_cell);
        if(1 >= distance)
        {
            score = 1;
        }

        int fighting_score = calc_cellcount_to_eat(to_cell, victim_cells);

        return score + fighting_score;
    }

    int calc_cellcount_to_eat(short cell, List<short> victim_cells)
    {
        List<short> cells_to_eat = Helper.find_neighbor_cells(cell, victim_cells, 1);
        return cells_to_eat.Count;
    }
}
