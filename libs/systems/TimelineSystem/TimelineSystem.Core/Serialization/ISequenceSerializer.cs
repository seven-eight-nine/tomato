namespace Tomato.TimelineSystem;

public interface ISequenceSerializer
{
    SequenceDto Serialize(Sequence sequence);
    Sequence Deserialize(SequenceDto dto, IClipFactory clipFactory);
}
