using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Helper {
    
    static Vector2 convert_to_xy(short cell)
    {
        return new Vector2(calc_row(cell), calc_col(cell));
    }
    
    public static short calc_row(short cell)
    {
        return (short)(cell / BattleRoom.COL_COUNT);
    }
    
    public static short calc_col(short cell)
    {
        return (short)(cell % BattleRoom.COL_COUNT);
    }
    
    //public static short get_position(byte row, byte col)
    //{
    //    return (short)(row * BattleRoom.COL_COUNT + col);
    //}
    
    public static short get_distance(short from, short to)
    {
        Vector2 pos1 = convert_to_xy(from);
        Vector2 pos2 = convert_to_xy(to);
        return get_distance(pos1, pos2);
    }
    public static byte get_distance(Vector2 pos1, Vector2 pos2)
    {
        Vector2 distance = pos1 - pos2;
        short x = (short)Mathf.Abs(distance.x);
        short y = (short)Mathf.Abs(distance.y);
        return (byte)Mathf.Max(x, y);
    }

    public static byte howfar_from_clicked_cell(short basis_cell, short cell)
    {
        short row = (short)(basis_cell / BattleRoom.COL_COUNT);
        short col = (short)(basis_cell % BattleRoom.COL_COUNT);
        Vector2 basic_pos = new Vector2(col, row);

        row = (short)(cell / BattleRoom.COL_COUNT);
        col = (short)(cell % BattleRoom.COL_COUNT);
        Vector2 cell_pos = new Vector2(col, row);

        Vector2 distance = basic_pos - cell_pos;
        short x = (short)Mathf.Abs(distance.x);
        short y = (short)Mathf.Abs(distance.y);
        return (byte)Mathf.Max(x, y);
    }
    
    public static List<short> find_neighbor_cells(short basis_cell, List<short> targets, short gap)
    {
        Vector2 pos = convert_to_xy(basis_cell);
        return targets.FindAll(obj => get_distance(pos, convert_to_xy(obj)) <= gap);
    }
    
    public static bool can_play_more(List<short> board, List<Player> players, int current_player_index)
    {
        Player current = players[current_player_index];
        foreach(byte cell in current.cell_indexes)
        {
            if (Helper.find_available_cells(cell, board, players).Count > 0)
            {
                return true;
            }
        }

        return false;
    }
    
    public static List<short> find_available_cells(short basis_cell, List<short> total_cells, List<Player> players)
    {
        List<short> targets = find_neighbor_cells(basis_cell, total_cells, 2);

        players.ForEach(obj =>
        {
            targets.RemoveAll(number => obj.cell_indexes.Exists(cell => cell == number));
        });

        return targets;
    }
}
