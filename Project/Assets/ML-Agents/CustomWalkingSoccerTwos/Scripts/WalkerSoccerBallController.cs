using UnityEngine;

public class WalkerSoccerBallController : MonoBehaviour
{
    public GameObject area;
    [HideInInspector]
    public WalkerSoccerEnvController envController;
    public string purpleGoalTag; //will be used to check if collided with purple goal
    public string blueGoalTag; //will be used to check if collided with blue goal

    void Start()
    {
        envController = area.GetComponent<WalkerSoccerEnvController>();
    }

    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag(purpleGoalTag)) //ball touched purple goal
        {
            Debug.Log("Goal for Blue Team");
            envController.GoalTouched(Team.Blue);
        }
        if (col.gameObject.CompareTag(blueGoalTag)) //ball touched blue goal
        {
            Debug.Log("Goal for Purple Team");
            envController.GoalTouched(Team.Purple);
        }
    }
}
