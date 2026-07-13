import { useState } from "react";
import { createFeedback } from "../../services/apiService";

export default function Feedback() {
  const [message, setMessage] = useState("");
  const [rating, setRating] = useState("");
  const [result, setResult] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();

    try {
      await createFeedback({
        message,
        rating: rating === "" ? null : Number(rating),
      });

      setResult("Geri bildiriminiz başarıyla gönderildi.");
      setMessage("");
      setRating("");
    } catch (err) {
      setResult("Gönderme işlemi başarısız.");
    }
  };

  return (
    <div style={{ padding: "40px", maxWidth: "600px", margin: "auto" }}>
      <h2>Geri Bildirim</h2>

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: "20px" }}>
          <label>Mesaj</label>

          <textarea
            rows="6"
            style={{ width: "100%", marginTop: "10px" }}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            required
          />
        </div>

        <div style={{ marginBottom: "20px" }}>
          <label>Puan (1-5)</label>

          <select
            value={rating}
            onChange={(e) => setRating(e.target.value)}
            style={{ width: "100%", padding: "10px", marginTop: "10px" }}
          >
            <option value="">Puan vermek istemiyorum</option>
            <option value="1">1</option>
            <option value="2">2</option>
            <option value="3">3</option>
            <option value="4">4</option>
            <option value="5">5</option>
          </select>
        </div>

        <button type="submit">
          Gönder
        </button>
      </form>

      {result && (
        <p style={{ marginTop: "20px", fontWeight: "bold" }}>
          {result}
        </p>
      )}
    </div>
  );
}