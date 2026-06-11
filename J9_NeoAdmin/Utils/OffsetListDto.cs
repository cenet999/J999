public record OffsetListDto<T>(long? Offset, IEnumerable<T> List);
