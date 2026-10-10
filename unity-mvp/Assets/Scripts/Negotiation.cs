using System.Collections.Generic;
using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Negociação por regras (funciona sem LLM). O executivo tem um preço de reserva secreto que depende do
    // desconto máximo do produto, de volume, prazo e argumentos, e endurece se o jogador sempre negocia no limite.
    public class Negotiation
    {
        public enum State { Open, Closed, Lost }
        public enum Argument { Avista, Case, Concorrente }

        public readonly Person Npc;
        public readonly BusinessSeed.Product Product;
        public int Quantity { get; private set; } = 1;
        public int TermMonths { get; private set; } = 12;
        public float Ask { get; private set; }           // preço unitário pedido pelo executivo
        public float LastOffer { get; private set; }
        public int Round { get; private set; }
        public int Patience { get; private set; } = 6;
        public State Status { get; private set; } = State.Open;
        public readonly HashSet<Argument> UsedArguments = new HashSet<Argument>();

        readonly TemplateContext ctx;
        readonly float toughness; // 0..0.05 a menos de margem por histórico
        float argumentBonus;

        public Negotiation(Person npc, PlayerProfile profile, BusinessSeed.Product product, float pastConcession)
        {
            Npc = npc;
            Product = product;
            Ask = product.price;
            ctx = new TemplateContext(npc, profile, product);
            toughness = Mathf.Clamp01(pastConcession / Mathf.Max(0.01f, product.maxDiscount)) * 0.04f;
        }

        // Desconto máximo que o executivo aceita nas condições atuais.
        public float MaxDiscount =>
            Mathf.Max(0.02f, Product.maxDiscount
                + (Quantity >= Product.volume ? 0.05f : 0)
                + (Product.Monthly && TermMonths >= 24 ? 0.03f : 0)
                + (Product.Monthly && TermMonths >= 36 ? 0.02f : 0)
                + argumentBonus - toughness);

        float Reservation => Product.price * (1 - MaxDiscount);
        public float Total(float unitPrice) => unitPrice * Quantity * (Product.Monthly ? TermMonths : 1);

        public string Opening() => ctx.FillVariant(string.Join("||", BusinessSeed.Instance.NegotiationLines("abertura")));

        string Line(string key, float counter = 0)
        {
            ctx.Set("contra", Money.Format(counter));
            ctx.Set("preco_final", Money.Format(counter));
            return ctx.FillVariant(string.Join("||", BusinessSeed.Instance.NegotiationLines(key)));
        }

        public string SetQuantity(int q)
        {
            bool crossed = q >= Product.volume && Quantity < Product.volume;
            Quantity = Mathf.Max(1, q);
            return crossed ? Line("argumento_volume") : null;
        }

        public string SetTerm(int months)
        {
            bool longer = months > TermMonths && months >= 24;
            TermMonths = months;
            return longer ? Line("argumento_prazo") : null;
        }

        public string UseArgument(Argument arg)
        {
            if (!UsedArguments.Add(arg)) return null;
            switch (arg)
            {
                case Argument.Avista: argumentBonus += 0.02f; return Line("argumento_avista");
                case Argument.Case: argumentBonus += 0.02f; return Line("argumento_case");
                default: argumentBonus += 0.03f; Patience -= 1; return Line("argumento_concorrente");
            }
        }

        // O jogador propõe um preço unitário; devolve a fala do executivo.
        public string Offer(float unitPrice)
        {
            if (Status != State.Open) return null;
            Round++;
            LastOffer = unitPrice;
            float reservation = Reservation;

            if (unitPrice >= Ask - Product.price * 0.005f) return Close(unitPrice);
            if (unitPrice >= reservation)
            {
                // Concede metade da distância; fecha se a diferença ficar pequena.
                float next = Mathf.Max(reservation, Ask - (Ask - unitPrice) * 0.5f);
                if (next - unitPrice <= Product.price * 0.01f) return Close(unitPrice);
                Ask = Round2(next);
                Patience--;
                return Patience <= 0 ? Walk() : Line("contraproposta", Ask);
            }
            // Abaixo da reserva: concede pouco e perde paciência mais rápido se a oferta for muito baixa.
            Ask = Round2(Mathf.Max(reservation * 1.02f, Ask - (Ask - reservation) * 0.25f));
            Patience -= unitPrice < reservation * 0.9f ? 2 : 1;
            if (Patience <= 0) return Walk();
            return Line(Patience <= 2 ? "impaciencia" : "rejeicao", Ask);
        }

        public string AcceptAsk() => Status == State.Open ? Close(Ask) : null;

        string Close(float price)
        {
            Status = State.Closed;
            Ask = Round2(price);
            LocalModel.Instance.SaveDeal(ToDeal("Fechado"));
            return Line("aceite", Ask);
        }

        string Walk()
        {
            Status = State.Lost;
            LocalModel.Instance.SaveDeal(ToDeal("Perdido"));
            return Line("desistencia");
        }

        DealRow ToDeal(string status) => new DealRow
        {
            Npc = Npc.Name, Company = Product.company, Product = Product.name, Unit = Product.unit, Status = status,
            ListPrice = Product.price, FinalPrice = status == "Fechado" ? Ask : 0, Total = status == "Fechado" ? Total(Ask) : 0,
            Quantity = Quantity, TermMonths = Product.Monthly ? TermMonths : 0, Rounds = Round,
        };

        static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
    }
}
