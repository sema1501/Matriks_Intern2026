import { useState } from "react";
import { createFeedback } from "../../services/apiService";
import { useLanguage } from "../../context/LanguageContext";

export default function Feedback() {
  const { t } = useLanguage();
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

      setResult(t('feedback.success'));
      setMessage("");
      setRating("");
    } catch (err) {
      setResult(t('feedback.error'));
    }
  };

  return (
    <div style={{ padding: "40px", maxWidth: "600px", margin: "auto" }}>
      <h2>{t('feedback.title')}</h2>

      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: "20px" }}>
          <label>{t('feedback.message')}</label>

          <textarea
            rows="6"
            style={{ width: "100%", marginTop: "10px" }}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            required
          />
        </div>

        <div style={{ marginBottom: "20px" }}>
          <label>{t('feedback.rating')}</label>

          <select
            value={rating}
            onChange={(e) => setRating(e.target.value)}
            style={{ width: "100%", padding: "10px", marginTop: "10px" }}
          >
            <option value="">{t('feedback.noRating')}</option>
            <option value="1">1</option>
            <option value="2">2</option>
            <option value="3">3</option>
            <option value="4">4</option>
            <option value="5">5</option>
          </select>
        </div>

        <button type="submit">
          {t('feedback.send')}
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
