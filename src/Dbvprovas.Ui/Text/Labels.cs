using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Text;

// Textos da tela para os códigos dos contratos (pt-BR). Os motivos repetem a tabela confirmada na D-160.
public static class Labels
{
    public static string Of(RoleCode value) => value switch
    {
        RoleCode.Director => "Diretor",
        RoleCode.AssociateDirector => "Diretor associado",
        RoleCode.Secretary => "Secretário",
        RoleCode.Treasurer => "Tesoureiro",
        RoleCode.Chaplain => "Capelão",
        RoleCode.Pastor => "Pastor",
        RoleCode.Counselor => "Conselheiro",
        RoleCode.AssociateCounselor => "Conselheiro associado",
        RoleCode.Instructor => "Instrutor",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string Of(ReasonCode value) => value switch
    {
        ReasonCode.IncompleteInformation => "Informações incompletas",
        ReasonCode.DuplicateRequest => "Solicitação duplicada",
        ReasonCode.ConditionsNotMet => "Condições de ingresso ou fundação não atendidas",
        ReasonCode.NewFoundingAfterPrevious => "Nova fundação após fundação anterior",
        ReasonCode.TemporaryLeave => "Afastamento temporário",
        ReasonCode.AdministrativeReview => "Revisão administrativa da participação",
        ReasonCode.RemovalRequested => "Encerramento da participação solicitado",
        ReasonCode.AdministrativeClosure => "Encerramento administrativo",
        ReasonCode.ReturnAfterLeave => "Retorno após afastamento",
        ReasonCode.SuspensionCorrection => "Correção de suspensão",
        ReasonCode.DirectorUnavailable => "Diretor indisponível",
        ReasonCode.DirectorshipRegularization => "Regularização da direção",
        ReasonCode.ClubSupportRequest => "Suporte solicitado pelo clube",
        ReasonCode.RegistrationInconsistency => "Verificação de inconsistência cadastral",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string Of(Sex value) => value == Sex.Female ? "Feminino" : "Masculino";

    public static string Of(MembershipStatus value) => value switch
    {
        MembershipStatus.Active => "Ativa",
        MembershipStatus.Suspended => "Suspensa",
        _ => "Encerrada",
    };

    public static string Of(UnitStatus value) => value == UnitStatus.Active ? "Ativa" : "Inativa";

    public static string Of(FoundingRequestStatus value) => value switch
    {
        FoundingRequestStatus.Pending => "Pendente",
        FoundingRequestStatus.Approved => "Aprovado",
        FoundingRequestStatus.Rejected => "Recusado",
        _ => "Cancelado",
    };

    public static string Of(JoinRequestStatus value) => value switch
    {
        JoinRequestStatus.Queued => "Na fila",
        JoinRequestStatus.Approved => "Aprovado",
        JoinRequestStatus.Rejected => "Recusado",
        JoinRequestStatus.Expired => "Expirado",
        _ => "Cancelado",
    };

    public static string Of(NominationStatus value) => value switch
    {
        NominationStatus.Pending => "Pendente",
        NominationStatus.Accepted => "Aceita",
        NominationStatus.Declined => "Recusada",
        NominationStatus.Cancelled => "Cancelada",
        _ => "Expirada",
    };

    public static string Of(AdminAccessStatus value) => value switch
    {
        AdminAccessStatus.Pending => "Pendente",
        AdminAccessStatus.Authorized => "Autorizado",
        AdminAccessStatus.Denied => "Negado",
        AdminAccessStatus.Expired => "Expirado",
        _ => "Revogado",
    };

    public static string Of(AdminDataField value) => value switch
    {
        AdminDataField.AdultProfile => "Perfil (nome, nascimento e sexo)",
        AdminDataField.Email => "E-mail da conta",
        AdminDataField.Memberships => "Participação e cargos",
        AdminDataField.Consents => "Consentimentos",
        _ => "Pedidos de entrada",
    };

    public static string Of(AuditAction value) => value switch
    {
        AuditAction.FoundingRequested => "Pedido de fundação enviado",
        AuditAction.FoundingCancelled => "Pedido de fundação cancelado",
        AuditAction.FoundingApproved => "Fundação aprovada",
        AuditAction.FoundingRejected => "Fundação recusada",
        AuditAction.JoinRequested => "Pedido de entrada enviado",
        AuditAction.JoinCancelled => "Pedido de entrada cancelado",
        AuditAction.JoinApproved => "Entrada aprovada",
        AuditAction.JoinRejected => "Entrada recusada",
        AuditAction.JoinExpired => "Pedido de entrada expirado",
        AuditAction.RoleAssigned => "Cargo atribuído",
        AuditAction.RoleEnded => "Cargo encerrado",
        AuditAction.DirectorNominated => "Sucessor indicado",
        AuditAction.DirectorNominationCancelled => "Indicação cancelada",
        AuditAction.DirectorNominationDeclined => "Indicação recusada",
        AuditAction.DirectorChanged => "Direção transferida",
        AuditAction.DirectorAppointed => "Diretor nomeado pelo admin",
        AuditAction.DirectorReplaced => "Diretor substituído pelo admin",
        AuditAction.MembershipSuspended => "Participação suspensa",
        AuditAction.MembershipReactivated => "Participação reativada",
        AuditAction.MembershipRemoved => "Participação removida",
        AuditAction.MembershipLeft => "Saída do clube",
        AuditAction.MembershipEnded => "Participação encerrada",
        AuditAction.ConsentGranted => "Consentimento dado",
        AuditAction.ConsentRevoked => "Consentimento revogado",
        AuditAction.AdminAccessRequested => "Acesso do admin pedido",
        AuditAction.AdminAccessAuthorized => "Acesso do admin autorizado",
        AuditAction.AdminAccessDenied => "Acesso do admin negado",
        AuditAction.AdminAccessRevoked => "Acesso do admin revogado",
        AuditAction.AdminPersonalDataRead => "Leitura de dado pessoal pelo admin",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string Of(EndCause value) => value switch
    {
        EndCause.RemovedByBoard => "Retirado pela diretoria",
        EndCause.MembershipSuspended => "Participação suspensa",
        EndCause.MembershipEnded => "Participação encerrada",
        EndCause.ConsentRevoked => "Consentimento revogado",
        EndCause.BecameDirector => "Passou a diretor",
        EndCause.DirectorTransferred => "Direção transferida",
        EndCause.DirectorReplaced => "Diretor substituído",
        EndCause.Left => "Saída",
        EndCause.FoundedNewClub => "Fundou outro clube",
        EndCause.Expired => "Prazo vencido",
        EndCause.RequestClosed => "Pedido encerrado",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static string Of(NotificationEvent value) => value switch
    {
        NotificationEvent.FoundingApproved => "Seu pedido de fundação foi aprovado",
        NotificationEvent.FoundingRejected => "Seu pedido de fundação foi recusado",
        NotificationEvent.JoinQueued => "Novo pedido de entrada na fila",
        NotificationEvent.JoinExpiringSoon => "Pedido de entrada perto de expirar",
        NotificationEvent.JoinApproved => "Seu pedido de entrada foi aprovado",
        NotificationEvent.JoinRejected => "Seu pedido de entrada foi recusado",
        NotificationEvent.JoinExpired => "Seu pedido de entrada expirou",
        NotificationEvent.DirectorNominated => "Você foi indicado para diretor",
        NotificationEvent.DirectorChanged => "A direção do clube mudou",
        NotificationEvent.ConsentRevoked => "Um membro revogou o consentimento",
        NotificationEvent.AdminAccessRequested => "O admin pediu acesso a dados do clube",
        NotificationEvent.AdminAccessAuthorized => "Seu pedido de acesso foi autorizado",
        NotificationEvent.AdminAccessDenied => "Seu pedido de acesso foi negado",
        NotificationEvent.AdminAccessRevoked => "Seu acesso foi revogado",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    // Códigos comuns; cada página trata os do seu módulo antes de cair aqui.
    public static string Problem(string? code) => code switch
    {
        ProblemCodes.ValidationText => "Confira o texto: ele não pode ficar vazio, passar do tamanho nem ter caracteres especiais.",
        ProblemCodes.ValidationDate => "Confira a data.",
        ProblemCodes.ReasonRequired => "Escolha um motivo da lista.",
        ProblemCodes.PermissionDenied => "Você não tem permissão para isso.",
        ProblemCodes.SelfAction => "Essa ação não pode ser feita sobre você mesmo.",
        ProblemCodes.StateConflict => "Isso já foi decidido ou mudou enquanto você olhava. Atualize a página.",
        ProblemCodes.ProfileIncomplete => "Complete o seu cadastro antes.",
        ProblemCodes.AgeBelowMinimum => "A idade mínima não foi atingida.",
        ProblemCodes.ConsentRequired => "É preciso aceitar o termo.",
        ProblemCodes.ConsentOutdated => "O termo mudou. Leia e aceite de novo.",
        ProblemCodes.PendingRequestExists => "Você já tem um pedido pendente.",
        _ => "Não foi possível concluir. Tente de novo.",
    };
}
