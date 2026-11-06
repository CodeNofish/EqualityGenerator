namespace EqualityGenerator.Tests {

  [Equatable]
  public partial class Player {
    public PlayerId Id { get; }
    public int Level { get; private set; }
    
    public Player(PlayerId id, int level) {
      Id = id;
    }
  }
  
  [Equatable]
  public partial class PlayerId {
    public string Value { get; }

    public PlayerId(string value) {
      Value = value;
    }
  }

  public class DemoTest {
    
  }

}
