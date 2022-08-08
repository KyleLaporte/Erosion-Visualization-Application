using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Undo : MonoBehaviour
{
    float[] currentHeightmap;
    LinkedList<float[]> changeList = new LinkedList<float[]>();
    LinkedListNode<float[]> current;
    LinkedListNode<float[]> previous;
    LinkedListNode<float[]> next;


    void SaveHeightmap()
    {
        // If we're not on the last node and are making difference changes, we need to delete the other states first
        if (current.Next != null)
        {
            // Remove the last entry until our current node is the last node
            while (current != changeList.Last)
            {
                changeList.RemoveLast();
            }
        }

        // Add heightmap to last position
        changeList.AddLast(currentHeightmap);
        current = changeList.Last;
    }
    void Undo_()
    {
        // If we have no previous state we can't go back to it
        if(current.Previous == null)
        {
            Debug.Log("Don't let button be clickable");
            return;
        }
        // Otherwise go back to our previous state
        currentHeightmap = current.Previous.Value;
        current = current.Previous;
    }
    void Redo()
    {
        // If we have no next state we can't go back to it
        if(current.Next == null)
        {
            Debug.Log("Don't let button be clickable");
            return;
        }
        // Otherwise go back to our next state
        currentHeightmap = current.Next.Value;
        current = current.Next;
    }
}
