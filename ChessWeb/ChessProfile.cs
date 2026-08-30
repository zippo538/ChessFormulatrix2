using AutoMapper;
using ChessConsole.Enums;
using ChessConsole.Model;
using ChessWeb.DTO;

namespace ChessAPI.Profiles;

public class ChessProfile : Profile
{
    public ChessProfile()
    {
        // Konfigurasi Postition[cite: 18]
        CreateMap<Postition, PosititionDTO>()
            .ForMember(dest => dest.Row, opt => opt.MapFrom(src => src.row))
            .ForMember(dest => dest.Column, opt => opt.MapFrom(src => src.Column));
        
        CreateMap<PosititionDTO, Postition>()
            .ForMember(dest => dest.row, opt => opt.MapFrom(src => src.Row))
            .ForMember(dest => dest.Column, opt => opt.MapFrom(src => src.Column));

        // Konfigurasi Piece[cite: 16]
        CreateMap<Piece, PieceDto>()
            .ForMember(
                destination => destination.PieceColor,
                option => option.MapFrom(source => source.PieceColor.ToString())
            )
            .ForMember(
                destination => destination.Type,
                option => option.MapFrom(source => source.Type.ToString())
            )
            .ForMember(
                destination => destination.HasMoved,
                option => option.MapFrom(source => source.Hasmove)
            );

        // Konfigurasi Tile[cite: 12]
        CreateMap<Tile, TileDto>();

        // Konfigurasi Board[cite: 15]
        CreateMap<Board, BoardDTO>();

        
        CreateMap<Move, MoveDTO>()
            .ForMember(dest => dest.PromotionType, opt => opt.MapFrom(src => src.PromptionType))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type));

        CreateMap<MoveDTO, Move>()
            .ForMember(
                dest => dest.PromptionType,
                opt => opt.MapFrom(src => src.PromotionType ?? PieceType.Queen))
            .ForMember(
                dest => dest.Type,
                opt => opt.MapFrom(src => src.Type ?? MoveType.Normal));

        // Konfigurasi MoveHistory[cite: 13]
        CreateMap<MoveHistory, MoveHistoryDTO>()
            .ForMember(dest => dest.PieceType, opt => opt.MapFrom(src => src.PieceType))
            .ForMember(dest => dest.PieceColor, opt => opt.MapFrom(src => src.PieceColor))
            .ForMember(dest => dest.CapturedPieceType, opt => opt.MapFrom(src => src.CapturedPieceType));

        // Konfigurasi Player[cite: 14]
        CreateMap<Player, PlayerDTO>()
            .ForMember(dest => dest.PlayerColor, opt => opt.MapFrom(src => src.PlayerColor.ToString()));
    }
}