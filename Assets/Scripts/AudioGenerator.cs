using UnityEngine;

public class AudioGenerator : MonoBehaviour
{
    public AudioClip soundClip;

    /// <summary>
    /// Notifies all entities within range if an audio is played.
    /// </summary>
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GetComponent<AudioSource>().PlayOneShot(soundClip);
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var isHit = Physics.Raycast(ray, out hit);
            var point = hit.point;
            if (isHit)
            {
                var enemies = GameObject.FindObjectsOfType<HostileBehavior>();
                Debug.Log(enemies.Length);
                Debug.Log(point);
                foreach(var enemy in enemies)
                {
                    if(Vector3.Distance(enemy.transform.position, point) < 20)
                    {
                        enemy.blackboard.Set("HeardSound", true);
                        enemy.blackboard.Set("SoundPosition", point);
                    }                 
                }
            }

        }
    }
}
